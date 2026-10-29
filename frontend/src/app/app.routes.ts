import { Routes } from '@angular/router';
import { authGuard, pageGuard } from './core/auth';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then(m => m.Login) },
  { path: 'forgot-password', loadComponent: () => import('./pages/forgot-password').then(m => m.ForgotPassword) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', canActivate: [pageGuard('dashboard')], loadComponent: () => import('./pages/dashboard').then(m => m.Dashboard) },
      { path: 'rooms', canActivate: [pageGuard('rooms')], loadComponent: () => import('./pages/rooms').then(m => m.Rooms) },
      { path: 'invoices', canActivate: [pageGuard('invoices')], loadComponent: () => import('./pages/invoices').then(m => m.Invoices) },
      { path: 'financials', canActivate: [pageGuard('financials')], loadComponent: () => import('./pages/financials').then(m => m.Financials) },
      { path: 'properties', canActivate: [pageGuard('properties')], loadComponent: () => import('./pages/properties').then(m => m.Properties) },
      { path: 'tenancies', canActivate: [pageGuard('tenancies')], loadComponent: () => import('./pages/tenancies').then(m => m.Tenancies) },
      { path: 'complaints', canActivate: [pageGuard('complaints')], loadComponent: () => import('./pages/complaints').then(m => m.Complaints) },
      { path: 'repairs', canActivate: [pageGuard('repairs')], loadComponent: () => import('./pages/repairs').then(m => m.Repairs) },
      { path: 'employees', loadComponent: () => import('./pages/employees').then(m => m.Employees) },
      { path: 'profile', loadComponent: () => import('./pages/profile').then(m => m.Profile) },
    ],
  },
  { path: '**', redirectTo: '' },
];
