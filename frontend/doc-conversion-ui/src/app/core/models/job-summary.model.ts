import { JobStatus, OutputFormat, ErrorCode } from './job-status.enum';

export interface JobSummaryDto {
  id: string;
  sourceFileName: string;
  requestedFormat: OutputFormat;
  resolvedFormat?: OutputFormat;
  status: JobStatus;
  createdAt: string;
  completedAt?: string;
  errorCode?: ErrorCode;
  errorMessage?: string;
}
