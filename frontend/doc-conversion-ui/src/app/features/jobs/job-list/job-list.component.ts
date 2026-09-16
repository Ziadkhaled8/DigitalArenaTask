import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../../core/services/job.service';
import { JobSummaryDto } from '../../../core/models/job-summary.model';
import { JobStatus } from '../../../core/models/job-status.enum';
import { StatusBadgeComponent } from '../../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, StatusBadgeComponent],
  templateUrl: './job-list.component.html',
  styleUrl: './job-list.component.scss'
})
export class JobListComponent implements OnInit {
  private readonly jobService = inject(JobService);

  jobs: JobSummaryDto[] = [];
  isLoading = false;
  errorMessage: string | null = null;
  searchQuery = '';
  selectedStatus = 'ALL';

  ngOnInit() {
    this.loadJobs();
  }

  loadJobs() {
    this.isLoading = true;
    this.errorMessage = null;

    this.jobService.getJobs().subscribe({
      next: (data) => {
        this.jobs = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage =
          'Unable to connect to Document Conversion API service at ' +
          this.jobService.getPartDownloadUrl('test', 1).split('/parts')[0] +
          '. Make sure the backend is running.';
      }
    });
  }

  get filteredJobs(): JobSummaryDto[] {
    return this.jobs.filter((job) => {
      const matchesSearch =
        !this.searchQuery ||
        job.sourceFileName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
        job.id.toLowerCase().includes(this.searchQuery.toLowerCase());

      const matchesStatus =
        this.selectedStatus === 'ALL' || job.status === this.selectedStatus;

      return matchesSearch && matchesStatus;
    });
  }

  getCount(status: JobStatus): number {
    return this.jobs.filter((j) => j.status === status).length;
  }
}
