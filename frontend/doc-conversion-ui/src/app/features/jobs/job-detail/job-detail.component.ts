import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { JobService } from '../../../core/services/job.service';
import { JobDetailDto } from '../../../core/models/job-detail.model';
import { StatusBadgeComponent } from '../../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, StatusBadgeComponent],
  templateUrl: './job-detail.component.html',
  styleUrl: './job-detail.component.scss'
})
export class JobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobService = inject(JobService);

  jobId!: string;
  job: JobDetailDto | null = null;
  isLoading = false;
  fetchError: string | null = null;

  ngOnInit() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.jobId = id;
        this.loadJob();
      }
    });
  }

  loadJob() {
    this.isLoading = true;
    this.fetchError = null;

    this.jobService.getJobDetail(this.jobId).subscribe({
      next: (data) => {
        this.job = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.fetchError =
          'Could not load job details. The job may not exist or the API is unavailable.';
      }
    });
  }

  getDownloadUrl(partNumber: number): string {
    return this.jobService.getPartDownloadUrl(this.jobId, partNumber);
  }

  getMarkerClass(status: string): string {
    return `marker-${status.toLowerCase()}`;
  }

  getFileName(filePath: string): string {
    if (!filePath) return '';
    const parts = filePath.replace(/\\/g, '/').split('/');
    return parts[parts.length - 1];
  }

  formatSize(bytes: number): string {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
