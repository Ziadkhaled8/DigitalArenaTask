// This is a C# script to generate sample PDF files for testing
// Run with: dotnet script generate-samples.csx
// Or create them manually using the methods described below

// For the assessment, we need:
// 1. text-only.pdf - A PDF with text content only (no images)
// 2. text-with-images.pdf - A PDF with both text and embedded images
// 3. scanned-image-only.pdf - A PDF that is just a scanned image (no text layer)
// 4. corrupted.pdf - A deliberately corrupted file

// NOTE: These sample PDFs should be created manually or sourced from:
// - Text-only: Any simple text PDF exported from Word/Google Docs
// - Text with images: A document with logos/photos exported as PDF
// - Scanned: A photo/scan saved as PDF (no OCR applied)
// - Corrupted: Take any PDF and delete/scramble some bytes

Console.WriteLine("Sample PDF generation guide:");
Console.WriteLine("1. text-only.pdf: Export a text document from Word/Google Docs as PDF");
Console.WriteLine("2. text-with-images.pdf: Export a document with images as PDF");
Console.WriteLine("3. scanned-image-only.pdf: Save a photo/scan directly as PDF");
Console.WriteLine("4. corrupted.pdf: Created programmatically below");

// Create corrupted.pdf - just random bytes with a PDF header
var corruptedBytes = new byte[1024];
new Random(42).NextBytes(corruptedBytes);
// Add PDF magic bytes at start to make it look like a PDF initially
corruptedBytes[0] = (byte)'%';
corruptedBytes[1] = (byte)'P';
corruptedBytes[2] = (byte)'D';
corruptedBytes[3] = (byte)'F';
corruptedBytes[4] = (byte)'-';
File.WriteAllBytes("corrupted.pdf", corruptedBytes);
Console.WriteLine("Created corrupted.pdf");
