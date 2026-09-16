import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { JobSummaryDto } from '../models/job-summary.model';
import { JobDetailDto } from '../models/job-detail.model';
import { OutputFormat } from '../models/job-status.enum';

@Injectable({
  providedIn: 'root'
})
export class JobService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/jobs`;

  submitJob(file: File, requestedFormat: OutputFormat): Observable<JobSummaryDto> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('requestedFormat', requestedFormat);
    return this.http.post<JobSummaryDto>(this.baseUrl, formData);
  }

  getJobs(): Observable<JobSummaryDto[]> {
    return this.http.get<JobSummaryDto[]>(this.baseUrl);
  }

  getJobDetail(id: string): Observable<JobDetailDto> {
    return this.http.get<JobDetailDto>(`${this.baseUrl}/${id}`);
  }

  getPartDownloadUrl(jobId: string, partNumber: number): string {
    return `${this.baseUrl}/${jobId}/parts/${partNumber}/download`;
  }
}
