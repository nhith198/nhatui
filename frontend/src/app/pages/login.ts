import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../core/auth';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  template: `
    <form class="card login" (ngSubmit)="submit()">
      <h1>Quản lý nhà trọ</h1>
      <p class="mute">{{ mode() === 'in' ? 'Đăng nhập để quản lý nhà cho thuê của bạn.' : 'Tạo tài khoản chủ nhà.' }}</p>
      @if (mode() === 'up') {
        <label>Họ và tên<input name="fullName" [(ngModel)]="f.fullName" autocomplete="name" required maxlength="200"></label>
      }
      <label>Số điện thoại<input name="phone" type="tel" [(ngModel)]="f.phone" inputmode="tel" autocomplete="tel" required maxlength="30"></label>
      <label>Mật khẩu<input name="password" type="password" [(ngModel)]="f.password" [attr.autocomplete]="mode() === 'in' ? 'current-password' : 'new-password'" required minlength="8" (keyup.enter)="submit()"></label>
      @if (mode() === 'in') { <p class="mute"><a routerLink="/forgot-password">Quên mật khẩu?</a></p> }
      @if (err()) { <p class="err" role="alert">{{ err() }}</p> }
      <div class="acts">
        <button type="button" (click)="mode.set(mode() === 'in' ? 'up' : 'in')">{{ mode() === 'in' ? 'Tạo tài khoản' : 'Đã có tài khoản' }}</button>
        <button class="p" type="submit">{{ mode() === 'in' ? 'Đăng nhập' : 'Đăng ký' }}</button>
      </div>
    </form>
  `,
})
export class Login {
  private auth = inject(Auth);
  private router = inject(Router);
  mode = signal<'in' | 'up'>('in');
  err = signal('');
  f = { fullName: '', phone: '', password: '' };

  submit() {
    this.err.set('');
    const body = this.mode() === 'in'
      ? { phone: this.f.phone, password: this.f.password }
      : { fullName: this.f.fullName, phone: this.f.phone, password: this.f.password };
    this.auth.submit(this.mode(), body).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: e => this.err.set(e?.error?.message ?? (e?.error?.errors ? 'Vui lòng kiểm tra lại thông tin (mật khẩu tối thiểu 8 ký tự)' : 'Không thể kết nối máy chủ')),
    });
  }
}
