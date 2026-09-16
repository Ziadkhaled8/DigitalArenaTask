# Document Conversion & Splitting Service — Build Spec

**Context:** Take-home technical assessment for Senior Full Stack Engineer (Angular / .NET) — Digital Arena Solutions (DigiArenas). This doc captures the architecture and every design decision made before coding, so implementation stays consistent throughout.

---

## 1. Business Scenario (as given)

A back-office system prepares documents for handoff to an external recipient. A source PDF is converted into a requested output format. If the converted output exceeds a size limit, it is split into ordered, sequential parts. Every request is tracked end-to-end: status, timestamps, and outcome (including exceptions).

---

## 2. Stack

| Layer | Choice |
|---|---|
| Frontend | Angular (standalone components, no NgModules) |
| Backend | ASP.NET Core Web API (.NET 8) |
| Database | SQLite (file-based, survives restarts, no install overhead) |
| PDF reading | PdfPig (MIT-licensed, pure C#) |
| DOCX writing | DocumentFormat.OpenXml (Microsoft, free, no licensing ambiguity) |
| HTML writing | Custom lightweight renderer (no external dep needed) |
| Testing | xUnit |

**Rejected:** iText7 — not deprecated, but AGPL/commercial dual-licensing is friction not worth taking on for a submission. PyMuPDF/Python — out, staying single-stack in .NET.

---

## 3. Conversion: Two Output Pipelines

Both DOCX and HTML are supported outputs, but **which one is valid for a given document is determined by document content, not free user choice**:

- If the source PDF contains **zero image XObjects** → both DOCX and HTML are valid requested formats.
- If the source PDF contains **one or more image XObjects** → **HTML is the only valid requested format**. A DOCX request against such a document is rejected at intake (fail-fast, see §6) rather than silently falling back to HTML.

**Image detection rule:** a PDF image is identified structurally — a `/XObject` dictionary entry with `/Subtype /Image` — not by any visual/heuristic inspection. This is checked once per page during parsing.

### Conversion fidelity ceiling (explicit, stated in README)
Conversion is **rule-based and deterministic** — text is carried across faithfully, images/logos/signatures are copied byte-for-byte into the output, not re-rendered or OCR'd. It targets **faithful and readable**, not pixel-perfect visual fidelity: correct reading order, paragraph/heading structure inferred from font-size heuristics, images placed in correct relative position. True DOCX/HTML visual fidelity to the original PDF (exact spacing, font metrics, multi-column reflow) is explicitly out of scope — PDF has no semantic document structure to reconstruct that from, and chasing it would trade time away from the edge cases this assessment is actually scored on.

### Pipeline architecture

```
PDF bytes
   │
   ▼
PdfDocumentParser (PdfPig)
   │  → ParsedDocument: ordered list of Elements
   │      - HeadingElement(text, level)
   │      - ParagraphElement(text)
   │      - ImageElement(bytes, mimeType, position)
   │  → HasImages: bool  (any /XObject /Image found)
   ▼
Format Router
   ├─ requested = Html                         → always allowed
   ├─ requested = Docx AND HasImages = false    → allowed
   └─ requested = Docx AND HasImages = true     → UnsupportedFormatException (fail-fast)
   ▼
Renderer (per resolved format)
   - HtmlDocumentRenderer:  ParsedDocument → HTML bytes
   - DocxDocumentRenderer:  ParsedDocument → DOCX bytes (OpenXml)
```

One parser produces a single intermediate representation (`ParsedDocument`); each renderer is a pure function from that representation to output bytes. This keeps conversion and rendering decoupled, and means splitting (§4) operates on the same representation regardless of target format.

---

## 4. Splitting

**Algorithm: sequential greedy accumulation, in document order.**

- Walk `ParsedDocument` elements in original order.
- Add each element to the current part; after each addition, **render the part so far and measure its actual serialized byte size** (not an estimate — actual file bytes, since wrapper overhead differs between HTML and DOCX and between part 1 vs part N).
- If adding the next element would push the part **over** the limit, close the current part and start a new one with that element.
- **Never reorder elements** to pack more efficiently — document order is a correctness constraint.
- **Never split inside a single element** — an element (a paragraph, an image block) is atomic and always belongs entirely to one part.

### Limit
- Default: **2 MB**, configurable.
- Boundary rule: a part sized **exactly** at the limit is valid and does **not** force an extra split (`<=`, not `<`).

### Edge cases (explicit handling)

| Case | Behavior |
|---|---|
| Output exactly at limit | Single part, valid, no split forced |
| Single element (e.g. one large embedded image) larger than the limit on its own | The part **is allowed to exceed the limit**. Flagged via `ExceedsSizeLimit = true` on that `OutputPart`. Job status becomes `CompletedWithWarnings`, not `Failed` — an oversized-but-complete handoff is more useful downstream than an outright rejection over one large image. |
| Empty/zero-content document | `EmptyDocumentException` — "nothing to convert" is an exception, not a zero-part success. |
| Corrupted input (unopenable, or any page throws mid-parse) | Abort the whole job — no partial conversion attempted. `CorruptedFileException`. |

---

## 5. Validation

Runs after splitting, before a job can be marked `Completed`.

- **Part-count integrity**: parts form a contiguous, gapless sequence `1..N` with a consistent `TotalParts` value.
- **Content integrity**: comparing raw byte-size sums across parts is **not valid** — each part is a self-contained file with its own wrapper overhead (HTML boilerplate / DOCX package structure), so part sizes will never sum to the pre-split total even when nothing was lost. Instead: extract and concatenate the canonical *content* (text + image byte references) across all parts, hash it, and compare against a hash of the same canonical content taken from the pre-split `ParsedDocument`. Mismatch → validation failure → `FlaggedForReview`, not `Failed` (no exception occurred, the pipeline ran — the output just didn't verify).

---

## 6. Domain Model

### `ConversionJob`
```
Id                    Guid
SourceFileName        string
RequestedFormat       enum { Docx, Html }
ResolvedFormat        enum { Docx, Html }   // after format-router resolution
Status                enum JobStatus
CreatedAt / UpdatedAt / CompletedAt   DateTime
SourceFilePath        string   // local disk path
ErrorCode             enum?    // typed, nullable
ErrorMessage          string?  // human-readable, nullable
```

### `JobStatus`
```
Received → Converting → Splitting → ValidatingOutput →
    Completed
    CompletedWithWarnings   (oversized part accepted)
    FlaggedForReview        (post-split validation failed, no exception)
Failed                       (reachable from Converting/Splitting/ValidatingOutput on exception)
```

### `JobEvent` (append-only history/timeline — this is what the frontend renders)
```
Id, JobId, Status, Timestamp, Message, ErrorCode?
```

### `OutputPart`
```
Id, JobId, PartNumber, TotalParts, FilePath, SizeBytes, ExceedsSizeLimit (bool)
```

### Typed exceptions
```
UnsupportedFormatException   — requested format not valid for this document/content combo
CorruptedFileException       — PDF unopenable, or a page throws during parse
ScannedDocumentException     — no extractable text layer on any page (image-only)
EmptyDocumentException       — zero content after parsing
```

**Fail-fast rule**: requested output format is validated against document content (see §3 format router) *before* any conversion work starts — never discovered mid-pipeline.

---

## 7. Architecture (Clean Architecture layers)

```
/Domain
    Entities: ConversionJob, JobEvent, OutputPart
    Enums: JobStatus, OutputFormat, ErrorCode
    Exceptions: UnsupportedFormatException, CorruptedFileException,
                ScannedDocumentException, EmptyDocumentException
    Interfaces: IDocumentParser, IDocumentRenderer, IDocumentSplitter,
                IJobValidator, IFileStorageProvider

/Application
    JobOrchestrationService   — drives the state machine end to end
    DTOs / Commands / Queries

/Infrastructure
    PdfDocumentParser         (PdfPig)
    HtmlDocumentRenderer
    DocxDocumentRenderer      (DocumentFormat.OpenXml)
    DocumentSplitter
    JobValidator
    LocalDiskFileStorageProvider : IFileStorageProvider
    EF Core DbContext + SQLite

/Api
    JobsController: POST /jobs, GET /jobs, GET /jobs/{id}
```

### File storage
`IFileStorageProvider` (`Save`, `Get`, `Delete` by key) is the storage seam. Implemented today as `LocalDiskFileStorageProvider` (path stored in `ConversionJob`/`OutputPart` rows); designed so an `S3FileStorageProvider` can be dropped in later without touching domain or application logic. This boundary is worth calling out live in the interview — it's a direct answer to their "separation of concerns" criterion.

### Processing model
**Synchronous**, inline within the request, for this submission — deliberate scope decision given the 4-day window, to protect time for the edge cases that are explicitly scored closely. Noted in README as a "what I'd do differently" item: move to a background worker + queue (e.g. Hangfire) with polling or SignalR-pushed status updates, so large documents don't block the request thread.

---

## 8. API

| Endpoint | Purpose |
|---|---|
| `POST /jobs` | Submit a new conversion job (multipart file + requested format) |
| `GET /jobs` | List job history with status |
| `GET /jobs/{id}` | Job detail: full `JobEvent` timeline + `OutputPart` list with download links |

---

## 9. Frontend (Angular, standalone components)

- **Submit view** — file upload + output format selector, submits to `POST /jobs`.
- **History/list view** — table of jobs with status badges (color-coded per `JobStatus`).
- **Detail view** — timeline of `JobEvent`s for one job, plus list of `OutputPart`s with download links and an `ExceedsSizeLimit` indicator where relevant.

---

## 10. Testing scope

Standard unit testing (xUnit), focused where the assessment explicitly says it's scored closest:
- **Parser/converter**: text extraction correctness, image XObject detection (with/without images), scanned-document detection throws `ScannedDocumentException`, corrupted file throws `CorruptedFileException`.
- **Format router**: DOCX request rejected when document has images; HTML always accepted.
- **Splitter**: exact-at-limit boundary, oversized single element → `ExceedsSizeLimit` flag not a thrown exception, empty document → `EmptyDocumentException`, normal multi-part split produces correct ordered sequence.
- **Validator**: contiguous part sequence passes, gap in sequence fails, content-hash mismatch → `FlaggedForReview`.

No attempt to over-engineer coverage beyond this — API wiring and Angular UI are verified manually.

---

## 11. Assumptions & Decisions Log

1. Two output pipelines (DOCX + HTML); which is valid is determined by presence of image XObjects in the source PDF, not free user choice.
2. PdfPig over iText7 (licensing friction) and PyMuPDF (off-stack).
3. Conversion fidelity ceiling: faithful/readable structure, not pixel-perfect visual reproduction of the original PDF.
4. "Corrupted" = PDF fails to open, or any page throws during parse — no partial-document conversion attempted.
5. Requested-format validity checked before conversion starts (fail-fast).
6. Splitting is sequential/greedy in document order; elements are never split or reordered.
7. Size limit is 2 MB by default, configurable; boundary is inclusive (`<=`).
8. A single element exceeding the limit alone is allowed to exceed it in its own part (flagged, not rejected) — job completes as `CompletedWithWarnings`.
9. An empty/zero-content document is an exception (`EmptyDocumentException`), not a zero-part success.
10. Files stored on local disk, path persisted in DB; storage abstracted behind `IFileStorageProvider` for a future S3 implementation.
11. Post-split validation checks part-sequence contiguity + content-hash equivalence (not raw byte-size sums, which are invalid across per-part wrapper overhead).
12. Errors are typed (`ErrorCode` enum) with a human-readable message alongside, for both frontend rendering and history display.
13. Job status set: `Received, Converting, Splitting, ValidatingOutput, Completed, CompletedWithWarnings, FlaggedForReview, Failed`.
14. SQLite for persistence.
15. Synchronous processing now; background worker + queue is a stated future step.
16. Local disk file storage now, with DB-stored paths; S3 is the stated future step.
17. xUnit for testing.
18. Standard unit test coverage on parser/converter/splitter/validator only — not exhaustive.
19. Angular standalone components, no NgModules.

---

## 12. Build Plan (4 days)

- **Day 1** — Solution skeleton across all four layers; domain entities + EF Core migrations; state machine skeleton; `POST /jobs` endpoint; gather sample PDFs (clean text-only, text + images, image-only/scanned).
- **Day 2** — `PdfDocumentParser` + image XObject detection + format router; both renderers (HTML first, DOCX second); the three intake exception paths; unit tests alongside.
- **Day 3** — `DocumentSplitter` (greedy accumulation + all edge cases) + `JobValidator` (sequence + content-hash check); wire full orchestration end to end; start Angular submit/history/detail views.
- **Day 4** — Polish; README (stack choice, fidelity ceiling, assumptions list above, "what I'd do differently" — background processing, S3); fill remaining test gaps; dry-run the live demo.
