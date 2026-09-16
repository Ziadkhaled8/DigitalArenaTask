export type JobStatus =
  | 'Received'
  | 'Converting'
  | 'Splitting'
  | 'ValidatingOutput'
  | 'Completed'
  | 'CompletedWithWarnings'
  | 'FlaggedForReview'
  | 'Failed';

export type OutputFormat = 'Html' | 'Docx';

export type ErrorCode =
  | 'UnsupportedFormat'
  | 'CorruptedFile'
  | 'ScannedDocument'
  | 'EmptyDocument'
  | 'ValidationFailed'
  | 'Unknown';
