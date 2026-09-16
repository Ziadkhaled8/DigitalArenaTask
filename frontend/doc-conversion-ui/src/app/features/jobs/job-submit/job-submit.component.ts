import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { JobService } from '../../../core/services/job.service';
import { OutputFormat } from '../../../core/models/job-status.enum';
import { FileUploadComponent } from '../../../shared/file-upload/file-upload.component';

@Component({
  selector: 'app-job-submit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, FileUploadComponent],
  templateUrl: './job-submit.component.html',
  styleUrl: './job-submit.component.scss'
})
export class JobSubmitComponent {
  private readonly jobService = inject(JobService);
  private readonly router = inject(Router);

  selectedFile: File | null = null;
  requestedFormat: OutputFormat = 'Html';
  isSubmitting = false;
  errorMessage: string | null = null;

  onFileSelected(file: File | null) {
    this.selectedFile = file;
    this.errorMessage = null;
  }

  onSubmit() {
    if (!this.selectedFile) {
      this.errorMessage = 'Please select a PDF file to upload.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = null;

    this.jobService.submitJob(this.selectedFile, this.requestedFormat).subscribe({
      next: (summary) => {
        this.isSubmitting = false;
        this.router.navigate(['/jobs', summary.id]);
      },
      error: (err) => {
        this.isSubmitting = false;
        if (err.error && typeof err.error === 'string') {
          this.errorMessage = err.error;
        } else if (err.error && err.error.message) {
          this.errorMessage = err.error.message;
        } else {
          this.errorMessage = 'An error occurred while submitting the document. Please check the backend API connection.';
        }
      }
    });
  }
}
