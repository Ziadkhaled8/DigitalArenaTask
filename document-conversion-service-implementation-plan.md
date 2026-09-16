# Document Conversion & Splitting Service — Implementation Plan

Companion to `document-conversion-service-spec.md`. That doc is the architecture and decisions; this doc is the concrete build list — every project, file, class, and its job, backend and frontend, so nothing gets improvised mid-build.

---

## 0. Solution Structure

```
DocConversionService.sln
│
├── src/
│   ├── DocConversionService.Domain/
│   ├── DocConversionService.Application/
│   ├── DocConversionService.Infrastructure/
│   └── DocConversionService.Api/
│
├── tests/
│   ├── DocConversionService.Domain.Tests/
│   ├── DocConversionService.Application.Tests/
│   └── DocConversionService.Infrastructure.Tests/
│
├── frontend/
│   └── doc-conversion-ui/   (Angular workspace)
│
├── sample-files/
│   ├── text-only.pdf
│   ├── text-with-images.pdf
│   ├── scanned-image-only.pdf
│   └── corrupted.pdf
│
└── README.md
```

Reference direction: `Api → Application → Domain`, `Infrastructure → Application/Domain` (implements Application/Domain interfaces). `Domain` has zero dependencies on the other projects. `Api` is the only project referencing `Infrastructure` (for DI registration).

---

## 1. Domain Layer — `DocConversionService.Domain`

No external dependencies. Pure C#.

### 1.1 Entities (`/Entities`)

**`ConversionJob.cs`**
```
Guid Id
string SourceFileName
OutputFormat RequestedFormat
OutputFormat? ResolvedFormat
JobStatus Status
DateTime CreatedAt
DateTime UpdatedAt
DateTime? CompletedAt
string SourceFilePath
ErrorCode? ErrorCode
string? ErrorMessage
ICollection<JobEvent> Events
ICollection<OutputPart> Parts
```
Responsibility: aggregate root. Owns state transitions — expose `TransitionTo(JobStatus, string message)` that appends a `JobEvent` and updates `Status`/`UpdatedAt`, rather than letting callers mutate `Status` directly. This is the one place invalid transitions get caught (throw `InvalidOperationException` if the target state isn't reachable from current — keep a small static transition map).

**`JobEvent.cs`**
```
Guid Id
Guid JobId
JobStatus Status
DateTime Timestamp
string Message
ErrorCode? ErrorCode
```
Responsibility: append-only. No setters exposed outside the entity assembly — created only via `ConversionJob.TransitionTo`.

**`OutputPart.cs`**
```
Guid Id
Guid JobId
int PartNumber
int TotalParts
string FilePath
long SizeBytes
bool ExceedsSizeLimit
```

### 1.2 Enums (`/Enums`)

**`JobStatus.cs`**: `Received, Converting, Splitting, ValidatingOutput, Completed, CompletedWithWarnings, FlaggedForReview, Failed`

**`OutputFormat.cs`**: `Html, Docx`

**`ErrorCode.cs`**: `UnsupportedFormat, CorruptedFile, ScannedDocument, EmptyDocument, ValidationFailed, Unknown`

### 1.3 Exceptions (`/Exceptions`)

All extend a common `DocumentProcessingException(ErrorCode code, string message)` base so the orchestrator can catch one type and read `.ErrorCode` off it.

- `UnsupportedFormatException` — requested format invalid for this document's content
- `CorruptedFileException` — PDF unopenable, or a page throws during parse
- `ScannedDocumentException` — no extractable text layer found on any page
- `EmptyDocumentException` — zero content after parsing

### 1.4 Value objects / parsed model (`/Parsing`)

**`ParsedDocument.cs`** — `IReadOnlyList<DocumentElement> Elements`, `bool HasImages`

**`DocumentElement.cs`** (abstract base) with three concrete types:
- `HeadingElement(string Text, int Level)`
- `ParagraphElement(string Text)`
- `ImageElement(byte[] Bytes, string MimeType, int PageIndex)`

These live in Domain (not Infrastructure) because Application-layer splitting/validation logic needs to reason about elements without depending on PdfPig.

### 1.5 Interfaces (`/Interfaces`) — implemented in Infrastructure

```
IDocumentParser
    ParsedDocument Parse(byte[] pdfBytes)   // throws CorruptedFileException, ScannedDocumentException, EmptyDocumentException

IDocumentRenderer
    OutputFormat Format { get; }
    byte[] Render(IReadOnlyList<DocumentElement> elements)

IDocumentSplitter
    IReadOnlyList<SplitPartResult> Split(ParsedDocument doc, IDocumentRenderer renderer, long limitBytes)

IJobValidator
    ValidationResult Validate(ParsedDocument original, IReadOnlyList<SplitPartResult> parts)

IFileStorageProvider
    Task<string> SaveAsync(string key, byte[] content)
    Task<byte[]> GetAsync(string key)
    Task DeleteAsync(string key)

IJobRepository
    Task<ConversionJob?> GetByIdAsync(Guid id)
    Task<IReadOnlyList<ConversionJob>> GetAllAsync()
    Task AddAsync(ConversionJob job)
    Task SaveChangesAsync()
```

**`SplitPartResult.cs`** (plain record): `int PartNumber, int TotalParts, byte[] Content, bool ExceedsSizeLimit`
**`ValidationResult.cs`** (plain record): `bool IsValid, string? FailureReason`

---

## 2. Application Layer — `DocConversionService.Application`

References: Domain only. No EF Core, no PdfPig — this layer orchestrates against interfaces.

### 2.1 Orchestration (`/Jobs`)

**`JobOrchestrationService.cs`** — the pipeline conductor. Constructor-injects `IDocumentParser`, `IReadOnlyDictionary<OutputFormat, IDocumentRenderer>` (keyed renderer resolution), `IDocumentSplitter`, `IJobValidator`, `IFileStorageProvider`, `IJobRepository`, `IOptions<ConversionSettings>`.

Single public method: `Task<Guid> SubmitAndProcessAsync(SubmitJobCommand command)`

Internal steps (each wrapped so a thrown `DocumentProcessingException` transitions the job to `Failed` with the right `ErrorCode`, and any other unexpected exception also lands as `Failed`/`ErrorCode.Unknown` rather than bubbling as a 500 with no job record):
1. Create `ConversionJob` (`Status = Received`), persist, save source file via `IFileStorageProvider`.
2. Parse PDF → `ParsedDocument`. (`IDocumentParser.Parse` — this call is also where `ScannedDocumentException`/`EmptyDocumentException`/`CorruptedFileException` surface.)
3. **Format router**: if `command.RequestedFormat == Docx && parsedDoc.HasImages` → throw `UnsupportedFormatException` immediately (fail-fast, before rendering). Else `ResolvedFormat = command.RequestedFormat`.
4. Transition to `Converting`, resolve the matching `IDocumentRenderer`, render full document once (used for the "no split needed" path and as the validation baseline).
5. If rendered size ≤ limit → single `OutputPart` (PartNumber 1, TotalParts 1), skip to step 7.
6. Else transition to `Splitting`, call `IDocumentSplitter.Split(...)`, save each part via `IFileStorageProvider`, create `OutputPart` rows.
7. Transition to `ValidatingOutput`, call `IJobValidator.Validate(...)`.
8. Resolve final status:
   - validation invalid → `FlaggedForReview`
   - any part has `ExceedsSizeLimit = true` → `CompletedWithWarnings`
   - else → `Completed`
9. Set `CompletedAt`, persist.

**`SubmitJobCommand.cs`**: `byte[] FileContent, string FileName, OutputFormat RequestedFormat`

### 2.2 Queries (`/Jobs`)

**`JobQueryService.cs`**
```
Task<JobSummaryDto> GetSummaryAsync(Guid id)
Task<JobDetailDto> GetDetailAsync(Guid id)     // includes events + parts
Task<IReadOnlyList<JobSummaryDto>> GetAllAsync()
```

### 2.3 DTOs (`/Dtos`)

- `JobSummaryDto` — Id, SourceFileName, RequestedFormat, ResolvedFormat, Status, CreatedAt, CompletedAt
- `JobDetailDto` — everything in Summary + `IReadOnlyList<JobEventDto> Events`, `IReadOnlyList<OutputPartDto> Parts`, ErrorCode, ErrorMessage
- `JobEventDto` — Status, Timestamp, Message, ErrorCode
- `OutputPartDto` — PartNumber, TotalParts, SizeBytes, ExceedsSizeLimit, DownloadUrl

### 2.4 Configuration (`/Configuration`)

**`ConversionSettings.cs`** — `long MaxPartSizeBytes = 2 * 1024 * 1024` (bound from `appsettings.json`, overridable per environment).

### 2.5 Mapping

Manual static `MapToDto(ConversionJob)` extension methods in `/Dtos/JobMappingExtensions.cs` — skip AutoMapper, the shape is simple enough that a mapping library is unnecessary overhead for this scope.

---

## 3. Infrastructure Layer — `DocConversionService.Infrastructure`

References: Application, Domain, PdfPig, DocumentFormat.OpenXml, EF Core (Sqlite provider).

### 3.1 Parsing (`/Parsing`)

**`PdfDocumentParser.cs`** — implements `IDocumentParser`.
- Opens PDF via PdfPig; wraps open/parse in try/catch → rethrow as `CorruptedFileException` on any failure (both "won't open" and "page N throws").
- Per page: extract words/text via `page.GetWords()`; extract images via `page.GetImages()`.
- Image XObject detection: PdfPig's `Page.GetImages()` surfaces embedded image XObjects directly — this is exactly the `/XObject /Subtype /Image` structural check; no heuristic needed. `HasImages = parsedDocument.Elements.Any(e => e is ImageElement)`.
- If **all pages** produce zero words **and** at least one image exists → `ScannedDocumentException` (image-only, no text layer).
- If **zero elements total** (no text, no images, no pages) → `EmptyDocumentException`.
- Heading vs. paragraph heuristic: compare each text block's font size against the page's median/body font size (PdfPig exposes glyph font size); a block meaningfully larger → `HeadingElement`, else `ParagraphElement`. Keep this heuristic simple and documented — it's explicitly not meant to be exhaustive (see fidelity ceiling in the spec doc).
- Elements returned **in document/reading order** (page order, then top-to-bottom by Y-position within a page) — this ordering is what the splitter depends on.

### 3.2 Rendering (`/Rendering`)

**`HtmlDocumentRenderer.cs`** — implements `IDocumentRenderer` (`Format = Html`).
- Produces a single self-contained HTML file: `<h1>`/`<h2>` for headings by level, `<p>` for paragraphs, `<img src="data:{mime};base64,...">` for images (embedded inline as base64 so a part is a genuinely self-contained file — no separate asset files to lose track of during splitting/download).

**`DocxDocumentRenderer.cs`** — implements `IDocumentRenderer` (`Format = Docx`).
- Uses `DocumentFormat.OpenXml`: `WordprocessingDocument` with a `Body` containing one `Paragraph` per element (`Heading1`/`Heading2` style for headings, normal style for paragraphs) and a `Drawing`/`Blip` for each `ImageElement` via `ImagePart`.
- Only ever invoked when `HasImages == false` per the format router — so the image-embedding code path exists but is effectively unreachable in valid flows; still write it correctly since the router logic is what's actually gating it, not the renderer's capability.

### 3.3 Splitting (`/Splitting`)

**`DocumentSplitter.cs`** — implements `IDocumentSplitter`.
- Greedy accumulation per the spec: iterate elements, maintain a growing `List<DocumentElement> currentPartElements`; after each addition, call `renderer.Render(currentPartElements)` and check `.Length`. If over `limitBytes` **and** the part has more than one element, remove the last element, close the part, start a new part with that element.
- If a single element alone renders over `limitBytes`, close it as its own part with `ExceedsSizeLimit = true` rather than looping forever trying to shrink it further.
- After all parts are built, backfill `TotalParts` on every `SplitPartResult` (unknown until the full pass completes).
- Re-rendering per element addition is O(n²)-ish in the worst case for very large documents — acceptable for assessment scope; note as a "future optimization" (incremental serialization) in the README's "what I'd do differently."

### 3.4 Validation (`/Validation`)

**`JobValidator.cs`** — implements `IJobValidator`.
- Part-sequence check: part numbers are `1..N` with no gaps, all sharing the same `TotalParts`.
- Content-integrity check: build a canonical content signature from the **original** `ParsedDocument` (e.g. concatenate each element's text, and for images a hash of the image bytes, in order) and a matching canonical signature reconstructed from the concatenation of all parts' elements (since each `SplitPartResult` was built from real `DocumentElement`s, not just raw file bytes — the splitter should retain the element list alongside the rendered bytes for exactly this reason; extend `SplitPartResult` with `IReadOnlyList<DocumentElement> Elements` if not already carrying it). Compare hashes (SHA-256 over the canonical string/bytes).
- Mismatch or sequence gap → `ValidationResult(false, reason)`.

### 3.5 Storage (`/Storage`)

**`LocalDiskFileStorageProvider.cs`** — implements `IFileStorageProvider`.
- Root directory from config (`StorageSettings.RootPath`, e.g. `./storage`), subfoldered by job id: `storage/{jobId}/source.pdf`, `storage/{jobId}/parts/part-1.html`.
- `SaveAsync` returns the relative path that gets persisted on the entity; `GetAsync` reads bytes for download; `DeleteAsync` for cleanup (not required by the brief, included for completeness/tests).

### 3.6 Persistence (`/Persistence`)

**`AppDbContext.cs`** — `DbSet<ConversionJob>`, `DbSet<JobEvent>`, `DbSet<OutputPart>`. Fluent configuration in `OnModelCreating` (or separate `IEntityTypeConfiguration<T>` classes per entity — prefer this for readability):
- `ConversionJobConfiguration.cs`
- `JobEventConfiguration.cs`
- `OutputPartConfiguration.cs`

Each configures keys, required fields, enum-to-string conversion (store enums as strings for readability in the SQLite file, not raw ints), and the `Job → Events`/`Job → Parts` one-to-many relationships with cascade delete.

**`JobRepository.cs`** — implements `IJobRepository`, thin wrapper over `AppDbContext` (`Include(j => j.Events).Include(j => j.Parts)` on reads).

**Migrations**: `InitialCreate` — generated via `dotnet ef migrations add InitialCreate`, applied on startup (`db.Database.Migrate()` in `Program.cs`) so a fresh clone "just runs."

---

## 4. API Layer — `DocConversionService.Api`

### 4.1 Controllers (`/Controllers`)

**`JobsController.cs`**
```
POST   /api/jobs          multipart/form-data: file, requestedFormat  → 202 Accepted, JobSummaryDto
GET    /api/jobs                                                      → 200, IReadOnlyList<JobSummaryDto>
GET    /api/jobs/{id}                                                 → 200, JobDetailDto | 404
GET    /api/jobs/{id}/parts/{partNumber}/download                     → 200, file stream | 404
```
Controller stays thin — validates the incoming multipart request shape (file present, size within a sane upload cap, format is a valid enum value), builds a `SubmitJobCommand`, and delegates to `JobOrchestrationService`/`JobQueryService`. No business logic here.

### 4.2 Middleware (`/Middleware`)

**`ExceptionHandlingMiddleware.cs`** — global catch for anything that escapes the orchestration service's own handling (it shouldn't, by design, but this is the safety net): maps `DocumentProcessingException` subtypes to `400 Bad Request` with `{ errorCode, message }`, everything else to `500` with a generic message (never leak stack traces to the client).

### 4.3 Startup (`Program.cs`)

- Register `AppDbContext` (Sqlite, connection string from config).
- Register `IJobRepository`, `IDocumentParser`, `IDocumentSplitter`, `IJobValidator`, `IFileStorageProvider` (all scoped/singleton as appropriate).
- Register both renderers and resolve them via a keyed dictionary: `IReadOnlyDictionary<OutputFormat, IDocumentRenderer>` built from `IEnumerable<IDocumentRenderer>` at startup (`.ToDictionary(r => r.Format)`).
- Bind `ConversionSettings`/`StorageSettings` from `appsettings.json` via `IOptions<T>`.
- CORS policy allowing the Angular dev origin (`http://localhost:4200`).
- Swagger/OpenAPI (`AddEndpointsApiExplorer` + `AddSwaggerGen`) — useful for you to manually exercise endpoints before the frontend is wired up.
- `app.UseMiddleware<ExceptionHandlingMiddleware>()`.
- `db.Database.Migrate()` on startup.

### 4.4 Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": { "Default": "Data Source=doc-conversion.db" },
  "Conversion": { "MaxPartSizeBytes": 2097152 },
  "Storage": { "RootPath": "./storage" }
}
```

---

## 5. Tests

### `DocConversionService.Domain.Tests`
- `ConversionJob` state-transition rules (valid transitions succeed, invalid ones throw).

### `DocConversionService.Application.Tests`
- `JobOrchestrationService` with faked `IDocumentParser`/`IDocumentSplitter`/`IJobValidator`/`IFileStorageProvider`/`IJobRepository` (Moq or NSubstitute):
  - happy path → `Completed`
  - parser throws `ScannedDocumentException` → job ends `Failed`, `ErrorCode.ScannedDocument`
  - DOCX requested + `HasImages = true` → `UnsupportedFormatException` before any render call
  - split produces an oversized part → `CompletedWithWarnings`
  - validator returns invalid → `FlaggedForReview`

### `DocConversionService.Infrastructure.Tests`
- `PdfDocumentParser` against the real sample PDFs in `/sample-files`:
  - text-only.pdf → elements extracted, `HasImages = false`
  - text-with-images.pdf → `HasImages = true`
  - scanned-image-only.pdf → throws `ScannedDocumentException`
  - corrupted.pdf → throws `CorruptedFileException`
- `DocumentSplitter`:
  - small doc → single part, no split
  - large doc → multiple parts, contiguous sequence, none exceed limit
  - one oversized element → single part flagged `ExceedsSizeLimit = true`
  - empty `ParsedDocument` → caller-level `EmptyDocumentException` (parser-level, but covered here as an integration check too)
  - exactly-at-limit content → not forced into an extra part
- `JobValidator`:
  - valid parts → `IsValid = true`
  - a gap in part numbering → `IsValid = false`
  - tampered/mismatched content → `IsValid = false`
- `LocalDiskFileStorageProvider`: save/get/delete round-trip on a temp directory.

---

## 6. Frontend — Angular Workspace (`frontend/doc-conversion-ui`)

Standalone components, no NgModules. Angular CLI defaults otherwise.

### 6.1 Structure

```
src/app/
├── app.routes.ts
├── app.config.ts
│
├── core/
│   ├── models/
│   │   ├── job-summary.model.ts
│   │   ├── job-detail.model.ts
│   │   ├── job-event.model.ts
│   │   ├── output-part.model.ts
│   │   └── job-status.enum.ts
│   └── services/
│       └── job.service.ts
│
├── shared/
│   ├── status-badge/
│   │   └── status-badge.component.ts
│   └── file-upload/
│       └── file-upload.component.ts
│
└── features/
    └── jobs/
        ├── job-submit/
        │   └── job-submit.component.ts
        ├── job-list/
        │   └── job-list.component.ts
        └── job-detail/
            └── job-detail.component.ts
```

### 6.2 Models (`/core/models`)

TypeScript interfaces mirroring the backend DTOs exactly (`JobSummaryDto`, `JobDetailDto`, `JobEventDto`, `OutputPartDto`), plus `JobStatus` and `OutputFormat` as string union types matching the backend enum string values (since enums are persisted/serialized as strings — see §3.6 — keep the frontend in lockstep with that, not with raw ints).

### 6.3 `JobService` (`/core/services/job.service.ts`)

Thin HttpClient wrapper, one method per endpoint:
```
submitJob(file: File, requestedFormat: OutputFormat): Observable<JobSummaryDto>
getJobs(): Observable<JobSummaryDto[]>
getJobDetail(id: string): Observable<JobDetailDto>
getPartDownloadUrl(jobId: string, partNumber: number): string
```
`submitJob` builds a `FormData` payload. No caching/state layer beyond this — components call the service directly and hold their own local state (`signal`/simple field), which is proportionate for three views and avoids introducing NgRx/state-management overhead the assessment doesn't call for.

### 6.4 Components

**`FileUploadComponent`** (shared) — drag/drop + click-to-browse file input, emits `fileSelected: File`. Dumb/presentational.

**`StatusBadgeComponent`** (shared) — `@Input() status: JobStatus`, renders a color-coded label (map each `JobStatus` to a color/label pair internally).

**`JobSubmitComponent`** (feature) — hosts `FileUploadComponent` + an output-format `<select>` (Html/Docx), a submit button, calls `JobService.submitJob`, shows a success/error toast-style message, and navigates to the job detail view on success.

**`JobListComponent`** (feature) — on init calls `JobService.getJobs()`, renders a table (filename, requested/resolved format, `StatusBadgeComponent`, created/completed timestamps), each row links to `JobDetailComponent`. Include a manual "Refresh" button — polling/auto-refresh is explicitly out of scope given the synchronous processing model (§7 of the spec doc — job is already resolved by the time the submit call returns, so there's nothing to poll for in this version).

**`JobDetailComponent`** (feature) — route param `id`, calls `JobService.getJobDetail(id)`, renders:
- summary header (filename, formats, status badge, error message if `Failed`/`FlaggedForReview`)
- `JobEvent` timeline (chronological list: status, timestamp, message)
- `OutputPart` list (part number/total, size, an `ExceedsSizeLimit` warning icon where true, download link per part via `getPartDownloadUrl`)

### 6.5 Routing (`app.routes.ts`)

```
'' → redirect to 'jobs'
'jobs' → JobListComponent
'jobs/submit' → JobSubmitComponent
'jobs/:id' → JobDetailComponent
```

### 6.6 Config (`app.config.ts`)

`provideHttpClient()`, `provideRouter(routes)`. API base URL in a simple `environment.ts`/`environment.prod.ts` pair (`apiUrl: 'http://localhost:5xxx/api'`).

---

## 7. Cross-Cutting / Easy-to-Miss Items

- **CORS** must be configured on the API before the Angular dev server can call it — do this on Day 1, not as an afterthought when the frontend is ready to test.
- **Upload size cap** on the API (`Kestrel`/`IISServerOptions` request body size limit, and `[RequestSizeLimit]` on the endpoint) — distinct from the 2MB *output* split limit; the *input* PDF itself needs a sane accepted-size ceiling too, or a large enough PDF will fail ungracefully before your own logic ever runs.
- **Enum serialization**: configure `System.Text.Json` to serialize enums as strings (`JsonStringEnumConverter`) so the API responses are human-readable and match what's stored in SQLite — keeps frontend, backend, and DB all speaking the same enum vocabulary.
- **File download endpoint** — don't forget this exists as its own controller action (§4.1); it's easy to build the submit/list/detail flow and realize download links have nowhere to point.
- **Sample PDFs** — these are a deliverable per the brief itself ("provide a handful of sample PDFs of your own"), not just test fixtures; keep them in `/sample-files` and reference them in the README.
- **README** — setup/run instructions for both projects (`dotnet ef database update`, `dotnet run`, `npm install`, `ng serve`), the assumptions list from the spec doc, and the "what I'd do differently" section (background processing + queue, S3 storage, splitter re-render performance).
- **.gitignore** — `bin/`, `obj/`, `node_modules/`, the SQLite file, and the `storage/` output directory (or keep storage but not the DB — your call, just be deliberate rather than accidentally committing generated job output).

---

## 8. Build Order Checklist

- [ ] Day 1: solution/project scaffolding, Domain entities + enums + exceptions + interfaces, EF Core DbContext + configurations + initial migration, `Program.cs` DI wiring, `JobsController` skeleton (routes return 501/stub), CORS, Swagger, sample PDFs gathered
- [ ] Day 2: `PdfDocumentParser` + both renderers + format router logic inside `JobOrchestrationService`, intake exception paths, Domain/Application/Infrastructure unit tests for parsing
- [ ] Day 3: `DocumentSplitter` + `JobValidator` + full orchestration wiring + `LocalDiskFileStorageProvider`, splitter/validator unit tests, Angular workspace scaffolded with all components/services/routing/models wired to a live backend
- [ ] Day 4: download endpoint, error handling middleware, enum-as-string JSON config, upload size cap, polish UI states (loading/error), README + assumptions + "what I'd do differently," full manual pass through all four sample PDFs, live-demo dry run
