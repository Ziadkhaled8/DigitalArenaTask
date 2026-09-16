import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'jobs',
    pathMatch: 'full'
  },
  {
    path: 'jobs',
    loadComponent: () =>
      import('./features/jobs/job-list/job-list.component').then(m => m.JobListComponent)
  },
  {
    path: 'jobs/submit',
    loadComponent: () =>
      import('./features/jobs/job-submit/job-submit.component').then(m => m.JobSubmitComponent)
  },
  {
    path: 'jobs/:id',
    loadComponent: () =>
      import('./features/jobs/job-detail/job-detail.component').then(m => m.JobDetailComponent)
  },
  {
    path: '**',
    redirectTo: 'jobs'
  }
];
