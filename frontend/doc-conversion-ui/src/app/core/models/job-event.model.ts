import { JobStatus, ErrorCode } from './job-status.enum';

export interface JobEventDto {
  id: string;
  jobId: string;
  status: JobStatus;
  timestamp: string;
  message: string;
  errorCode?: ErrorCode;
}
