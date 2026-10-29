import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, tap, throwError } from 'rxjs';
import { AccountProfile, cfg } from './models';

@Injectable({ providedIn: 'root' })
export class Auth {
  private http = inject(HttpClient);
  private router = inject(Router);
  token = signal<string | null>(localStorage.getItem('token'));
  name = signal(localStorage.getItem('name') ?? '');
  isEmployee = signal(localStorage.getItem('isEmployee') === 'true');
  pagePermissions = signal<string[]>([]);
  loggedIn = computed(() => !!this.token());

  submit(mode: 'in' | 'up', body: { fullName?: string; phone: string; password: string }) {
    return this.http.post<{ token: string; fullName: string; isEmployee: boolean }>(`${cfg.apiUrl}/auth/${mode === 'in' ? 'login' : 'register'}`, body).pipe(
      tap(r => {
        localStorage.setItem('token', r.token);
        localStorage.setItem('name', r.fullName);
        this.token.set(r.token);
        this.name.set(r.fullName);
        this.isEmployee.set(r.isEmployee);
        localStorage.setItem('isEmployee', String(r.isEmployee));
      }),
    );
  }

  forgotPassword(phone: string) {
    return this.http.post<void>(`${cfg.apiUrl}/auth/forgot-password`, { phone });
  }

  resetPassword(body: { phone: string; code: string; newPassword: string }) {
    return this.http.post<void>(`${cfg.apiUrl}/auth/reset-password`, body);
  }

  refreshAccount() {
    this.http.get<AccountProfile>(`${cfg.apiUrl}/account`).subscribe({
      next: account => this.updateAccountState(account),
      error: () => {},
    });
  }

  updateAccountState(account: { fullName: string; isEmployee: boolean; pagePermissions?: string[] }) {
    this.name.set(account.fullName);
    this.isEmployee.set(account.isEmployee);
    this.pagePermissions.set(account.pagePermissions ?? []);
    localStorage.setItem('name', account.fullName);
    localStorage.setItem('isEmployee', String(account.isEmployee));
  }

  canAccessPage(page: string) { return !this.isEmployee() || this.pagePermissions().includes(page); }

  checkPagePermission(page: string) {
    if (!this.isEmployee()) return of(true);
    return this.http.get<AccountProfile>(`${cfg.apiUrl}/account`).pipe(
      tap(account => this.updateAccountState(account)),
      map(account => account.pagePermissions.includes(page)),
    );
  }

  firstAllowedPage() {
    return ['dashboard', 'rooms', 'tenancies', 'invoices', 'complaints', 'repairs', 'financials', 'properties']
      .find(page => this.pagePermissions().includes(page)) ?? 'profile';
  }

  logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('name');
    localStorage.removeItem('isEmployee');
    this.token.set(null);
    this.name.set('');
    this.isEmployee.set(false);
    this.pagePermissions.set([]);
    this.router.navigateByUrl('/login');
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const t = auth.token();
  const authed = t ? req.clone({ setHeaders: { Authorization: `Bearer ${t}` } }) : req;
  return next(authed).pipe(
    catchError(e => {
      if (e.status === 401 && !req.url.includes('/auth/')) auth.logout();
      return throwError(() => e);
    }),
  );
};

export const authGuard: CanActivateFn = () => inject(Auth).loggedIn() || inject(Router).createUrlTree(['/login']);

export const pageGuard = (page: string): CanActivateFn => () => {
  const auth = inject(Auth);
  const router = inject(Router);
  if (!auth.loggedIn()) return router.createUrlTree(['/login']);
  return auth.checkPagePermission(page).pipe(
    map(allowed => allowed ? true : router.createUrlTree([`/${auth.firstAllowedPage()}`])),
    catchError(() => of(router.createUrlTree(['/profile']))),
  );
};
