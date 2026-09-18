# DIGIARENAS — Document Conversion & Splitting Service

A production-grade, enterprise-ready service designed for deterministic back-office document intake, rule-based conversion, sequential size-based splitting, and end-to-end audit tracking.

Built for the **Senior Full Stack Engineer Technical Assessment** at **Digital Arena Solutions**.

---

## 1. Architectural Overview & Boundaries

The backend implements **Clean Architecture (Onion/Hexagonal)** with strict boundary separation and unidirectional dependency flow:

```
├── backend/
│   ├── DocConversionService.sln
│   ├── src/
│   │   ├── DocConversionService.Domain/           # Core enterprise logic, entities, value objects, exceptions, enums
│   │   ├── DocConversionService.Application/      # Use case orchestration, DTOs, interfaces, commands, queries
│   │   ├── DocConversionService.Infrastructure/   # PdfPig parsing, HTML/DOCX renderers, SQLite EF Core, file storage
│   │   └── DocConversionService.Api/              # ASP.NET Core Web API, controllers, middleware, CORS, Swagger
│   │
│   └── tests/
│       ├── DocConversionService.Domain.Tests/          # State machine transition rules & entity invariant unit tests
│       ├── DocConversionService.Application.Tests/     # Mock-driven pipeline orchestration & edge-case unit tests
│       └── DocConversionService.Infrastructure.Tests/  # Integration tests (PdfPig, splitting, storage, validation)
│
├── frontend/doc-conversion-ui/                # Angular 19 standalone SPA (separated TS / HTML / SCSS)
└── sample-files/                              # Test PDFs (text-only, text-with-images, scanned, corrupted)
```

### Dependency Rules:
- **Domain**: Zero external dependencies. Owns `ConversionJob` aggregate root, state machine transitions, parsed document models (`ParsedDocument`, `DocumentElement`), and domain exceptions.
- **Application**: Coordinates use cases (`JobOrchestrationService`, `JobQueryService`). Defines interfaces (`IDocumentParser`, `IDocumentRenderer`, `IDocumentSplitter`, `IJobValidator`, `IFileStorageProvider`, `IJobRepository`).
- **Infrastructure**: Implements the Application/Domain interfaces. Contains external libraries (`UglyToad.PdfPig`, `DocumentFormat.OpenXml`, `Microsoft.EntityFrameworkCore.Sqlite`).
- **API**: Host layer configuring Dependency Injection, middleware, CORS, Swagger, and JSON serialization.

---

## 2. Key Architectural Decisions & Trade-Offs

### 2.1 Rule-Based / Deterministic Conversion vs OCR / AI
Per functional requirement §4.2:
- **Deterministic extraction** uses `UglyToad.PdfPig` to extract glyphs, font sizes, words, and raster image XObjects directly from PDF content streams.
- **Heading detection** is rule-based: words on the same vertical baseline are grouped into lines, and lines with font size >= 14pt (or >= 18pt for H1) are emitted as `HeadingElement(level)`.
- **Scanned-only documents**: If a document has no text elements across all pages, the parser explicitly throws `ScannedDocumentException` (mapped to `ErrorCode.ScannedDocument`), ensuring scanned documents are never silently converted or lossy-guessed.

### 2.2 Target Formats: HTML & DOCX
- **HTML (`HtmlDocumentRenderer`)**: Full-fidelity structured web representation with semantic `<h1>`-`<h6>`, `<p>`, and inline base64-encoded images (`<img src="data:image/png;base64,...">`). Self-contained single-file output that renders identically across any viewer.
- **DOCX (`DocxDocumentRenderer`)**: Native Word document created using `DocumentFormat.OpenXml`. Strictly follows the format guard: per specification rules, documents containing raster images reject DOCX conversion with `UnsupportedFormatException` because images cannot be guaranteed to fit the target format layout rules without lossy formatting.

### 2.3 Deterministic Size-Based Splitting (2 MB Threshold)
- Output size limit defaults to **2 MB (2,097,152 bytes)**, configurable in `appsettings.json`.
- When rendered output exceeds 2 MB, `DocumentSplitter` partitions the document's element hierarchy into sequential parts (`part-1.html`, `part-2.html`, etc.) so that each part is a syntactically complete, stand-alone HTML document.
- **Edge cases handled**:
  - Exactly at 2MB limit: Remains a single part without creating unnecessary splits.
  - Oversized single elements: If an individual embedded image exceeds 2MB by itself, it is emitted as its own isolated part with `ExceedsSizeLimit = true`, resulting in a `CompletedWithWarnings` status.

### 2.4 End-to-End Validation & Flagged For Review
- Before marking a job as completed, `JobValidator` verifies:
  1. Part sequence continuity (strictly 1 to N with no missing indices).
  2. Every part exists on storage and is non-empty.
  3. Cumulative content integrity (no duplicate parts or missing elements).
- Any discrepancy transitions the job to `FlaggedForReview` rather than `Completed`.

---

## 3. Frontend Architecture (Angular 19 & SCSS)

The frontend is an **Angular 19 Standalone Single Page Application** featuring:
- **Clean File Separation**: All components strictly separate TypeScript logic (`.component.ts`), HTML templates (`.component.html`), and scoped SCSS styles (`.component.scss`).
- **Reactive Navigation**:
  - `/jobs`: Overview table with stats counters (Total, Completed, Warnings, Flagged, Failed), search/filter by status and filename, manual refresh.
  - `/jobs/submit`: Drag-and-drop PDF intake, format selector with capability guidance, splitting notice, and validation feedback.
  - `/jobs/:id`: Deep audit inspection with summary metadata, interactive event timeline, and output part cards with direct download links.
- **Design System**: Dark modern tech theme with glassmorphism cards, glowing status pill indicators, micro-animations, and responsive layout.

---

## 4. Getting Started & Running Locally

### Prerequisites
- **.NET 9 SDK** (`dotnet --version` >= 9.0)
- **Node.js** v22.14.0+ and **npm** v11+

### Backend Setup
```bash
# 1. Navigate to the repository root
cd c:/Users/ziadk/source/repos/Ziadkhaled8/DigitalArenaTask

# 2. Run all unit and integration tests (42 passing)
dotnet test backend/DocConversionService.sln

# 3. Run the API (automatically runs migrations and listens on port 5000)
dotnet run --project backend/src/DocConversionService.Api/DocConversionService.Api.csproj
```
- Swagger UI available at: `http://localhost:5000/swagger`
- API Base URL: `http://localhost:5000/api`

### Frontend Setup
```bash
# 1. Navigate to frontend directory
cd frontend/doc-conversion-ui

# 2. Start the Angular development server
npm start
```
- Frontend application runs at: `http://localhost:4200`

---

## 5. Sample PDFs Guide

Located in `/sample-files`:

| File Name | Characteristics | Expected Outcome |
|---|---|---|
| `text-only.pdf` | Multi-page text with headers & paragraphs | **Completed** (HTML or DOCX). Single or multi-part depending on size. |
| `text-with-images.pdf` | Mixed document containing text and raster graphics | **Completed** for HTML (renders text + images). **Failed** (`UnsupportedFormat`) for DOCX. |
| `scanned-image-only.pdf` | Scanned raster page with zero extractable text layer | **Failed** with `ScannedDocumentException` (`ErrorCode.ScannedDocument`). |
| `corrupted.pdf` | Invalid/malformed binary data header | **Failed** with `CorruptedFileException` (`ErrorCode.CorruptedFile`). |

---

## 6. What I Would Do Differently in Production

1. **Asynchronous Background Processing with Message Queue**:
   - In this assessment solution, processing is synchronous during the intake request for deterministic testability. In high-throughput production, intake would immediately return `202 Accepted` with a Job ID, dispatch a message to RabbitMQ / Azure Service Bus / AWS SQS, and dedicated worker microservices would process conversions concurrently.
2. **Server-Sent Events (SSE) or SignalR WebSockets**:
   - Enable live real-time pipeline status updates on the Angular UI without requiring manual refreshes.
3. **Distributed Cloud Storage**:
   - Replace `LocalDiskFileStorageProvider` with Azure Blob Storage or AWS S3 using pre-signed temporary download URLs.
4. **Streaming PDF Splitting**:
   - Stream large multi-hundred-megabyte files in chunks to minimize memory pressure.
5. **Splitting performance on large documents**:
   The splitter's greedy accumulation loop re-renders the entire *current part*
   after every element it adds, to check the part's size against the limit.
   For a part with N elements, that's O(N²) total render calls rather than O(N) — each element added forces a full re-render of everything accumulated so far.

   This doesn't affect correctness — the produced parts are still exactly right — only performance on documents with a large number of elements per part.

   With more time, I'd track size incrementally instead of re-deriving it from a
   full render each iteration:
   - Render each element once, in isolation, and cache its own byte contribution.
   - Maintain a running total (sum of cached element sizes + a fixed wrapper-overhead
   constant computed once) and compare that against the limit — no re-render
   needed to make the comparison.
   - Only assemble the full part content when actually closing it, by concatenating
   the already-rendered element bytes.

   This is exact for HTML, since concatenating markup is truly additive — the
   incremental estimate equals the real final size. It's only an approximation
   for DOCX, because the Open XML package is a compressed zip container with
   shared parts, so byte contributions aren't strictly additive. A fully correct
   DOCX version would still need one verification render per *part* on close
   (not per element) to catch any drift from the estimate, which is still a large
   reduction from the current per-element re-render cost.

   Left as-is for this submission since it doesn't affect the correctness the
   assessment scores, and the sample documents provided are well within the range
   where this has any practical impact.