export interface OutputPartDto {
  id: string;
  jobId: string;
  partNumber: number;
  totalParts: number;
  filePath: string;
  sizeBytes: number;
  exceedsSizeLimit: boolean;
}
