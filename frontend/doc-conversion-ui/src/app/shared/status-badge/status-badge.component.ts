import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobStatus } from '../../core/models/job-status.enum';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.scss'
})
export class StatusBadgeComponent {
  @Input() status: JobStatus | undefined;
  @Input() customLabel?: string;
  @Input() disablePulse = false;

  get badgeClass(): string {
    if (!this.status) return 'badge-received';
    return `badge-${this.status.toLowerCase()}`;
  }

  get indicatorClass(): string {
    return this.disablePulse ? 'no-pulse' : 'indicator-active';
  }

  get label(): string {
    if (this.customLabel) {
      return this.customLabel;
    }
    switch (this.status) {
      case 'Received': return 'Received';
      case 'Converting': return 'Converting';
      case 'Splitting': return 'Splitting';
      case 'ValidatingOutput': return 'Validating';
      case 'Completed': return 'Completed';
      case 'CompletedWithWarnings': return 'Completed with Warnings';
      case 'FlaggedForReview': return 'Flagged for Review';
      case 'Failed': return 'Failed';
      default: return this.status || 'Unknown';
    }
  }
}
