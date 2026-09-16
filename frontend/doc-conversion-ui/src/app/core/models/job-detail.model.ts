import { JobStatus, OutputFormat, ErrorCode } from './job-status.enum';
import { JobEventDto } from './job-event.model';
import { OutputPartDto } from './output-part.model';

export interface JobDetailDto {
  id: string;
  sourceFileName: string;
  requestedFormat: OutputFormat;
  resolvedFormat?: OutputFormat;
  status: JobStatus;
  createdAt: string;
  completedAt?: string;
  errorCode?: ErrorCode;
  errorMessage?: string;
  events: JobEventDto[];
  parts: OutputPartDto[];
}
