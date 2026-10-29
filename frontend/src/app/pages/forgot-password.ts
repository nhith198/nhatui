import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { Auth } from '../core/auth';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink],
  template: `
    <form class="card login" (ngSubmit)="step() === 1 ? sendCode() : reset()">
      <h1>Quên mật khẩu</h1>
      @if (step() === 1) {
        <p class="mute">Nhập số điện thoại đã đăng ký để nhận mã xác nhận.</p>
        <label>Số điện thoại<input name="phone" type="tel" [(ngModel)]="phone" inputmode="tel" autocomplete="tel" required maxlength="30"></label>
      } @else {
        <p class="mute">Nhập mã 6 số đã gửi tới {{ phone }} và mật khẩu mới. Mã có hiệu lực trong 10 phút.</p>
        <label>Mã xác nhận<input name="code" inputmode="numeric" [(ngModel)]="code" autocomplete="one-time-code" required pattern="[0-9]{6}" maxlength="6"></label>
        <label>Mật khẩu mới<input name="newPassword" type="password" [(ngModel)]="newPassword" autocomplete="new-password" required minlength="8"></label>
      }
      @if (info()) { <p class="mute">{{ info() }}</p> }
      @if (err()) { <p class="err" role="alert">{{ err() }}</p> }
      <div class="acts">
        <a routerLink="/login">Quay lại đăng nhập</a>
        <button class="p" type="submit" [disabled]="busy()">{{ step() === 1 ? 'Gửi mã' : 'Đặt lại mật khẩu' }}</button>
      </div>
    </form>
  `,
})
export class ForgotPassword {
  private auth = inject(Auth);
  private router = inject(Router);
  step = signal<1 | 2>(1);
  busy = signal(false);
  err = signal('');
  info = signal('');
  phone = '';
  code = '';
  newPassword = '';

  sendCode() {
    this.send(this.auth.forgotPassword(this.phone), () => {
      this.step.set(2);
      this.info.set('Nếu số điện thoại đã đăng ký, mã xác nhận đã được gửi.');
    });
  }

  reset() {
    this.send(this.auth.resetPassword({ phone: this.phone, code: this.code, newPassword: this.newPassword }), () => {
      this.router.navigateByUrl('/login');
    });
  }

  private send(request: Observable<void>, onDone: () => void) {
    this.err.set('');
    this.busy.set(true);
    request.subscribe({
      next: () => { this.busy.set(false); onDone(); },
      error: e => { this.busy.set(false); this.err.set(e?.error?.message ?? 'Không thể kết nối máy chủ'); },
    });
  }
}
