import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { AccountProfile } from '../core/models';
import { Icon } from '../core/icon';
import { Alerts } from '../core/alerts';

@Component({
  selector: 'app-profile',
  imports: [FormsModule, Icon],
  template: `
    <div class="page-head"><div><h2>Hồ sơ cá nhân</h2><p class="mute">{{ auth.isEmployee() ? 'Nhân viên' : 'Chủ nhà' }}</p></div></div>
    @if (loading()) {
      <div class="empty" role="status">Đang tải hồ sơ…</div>
    } @else if (account()) {
      <form class="profile-form" #profileForm="ngForm" (ngSubmit)="profileForm.valid && save()">
        <h3>Thông tin tài khoản</h3>
        <label>Họ và tên<input name="fullName" [(ngModel)]="form.fullName" required maxlength="200" autocomplete="name"></label>
        <label>Số điện thoại<input name="phone" type="tel" inputmode="tel" [(ngModel)]="form.phone" required maxlength="30" pattern="^\\+?[0-9][0-9\\s().-]{7,14}$" autocomplete="tel"></label>
        <div class="password-section">
          <h3>Đổi mật khẩu</h3>
          <label>Mật khẩu hiện tại<input name="currentPassword" type="password" [(ngModel)]="form.currentPassword" [required]="!!form.newPassword" autocomplete="current-password"></label>
          <label>Mật khẩu mới<input name="newPassword" type="password" [(ngModel)]="form.newPassword" minlength="8" maxlength="128" autocomplete="new-password"></label>
          <label>Xác nhận mật khẩu mới<input name="confirmPassword" type="password" [(ngModel)]="form.confirmPassword" [required]="!!form.newPassword" minlength="8" maxlength="128" autocomplete="new-password"></label>
          @if (form.confirmPassword && form.confirmPassword !== form.newPassword) { <p class="err" role="alert">Mật khẩu xác nhận không khớp.</p> }
          @if (saveError()) { <p class="err" role="alert">{{ saveError() }}</p> }
        </div>
        <div class="acts"><button class="p" type="submit" [disabled]="saving() || profileForm.invalid || !passwordsMatch()"><app-icon name="save" />{{ saving() ? 'Đang lưu…' : 'Lưu hồ sơ' }}</button></div>
      </form>
    }
  `,
  styles: [`
    .profile-form{max-width:560px}
    .profile-form h3{font-size:15px;margin:0 0 12px}
    .password-section{border-top:1px solid var(--line);margin-top:24px;padding-top:20px}
  `],
})
export class Profile {
  api = inject(Api);
  auth = inject(Auth);
  private alerts = inject(Alerts);
  account = signal<AccountProfile | null>(null);
  loading = signal(true);
  saving = signal(false);
  saveError = signal('');
  form = { fullName: '', phone: '', currentPassword: '', newPassword: '', confirmPassword: '' };

  constructor() {
    this.api.account().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: account => {
        this.account.set(account);
        this.form.fullName = account.fullName;
        this.form.phone = account.phone ?? '';
      },
      error: error => this.api.say(this.api.errorMessage(error)),
    });
  }

  passwordsMatch() { return !this.form.newPassword || this.form.newPassword === this.form.confirmPassword; }

  save() {
    if (this.saving() || !this.passwordsMatch()) return;
    this.saving.set(true);
    this.saveError.set('');
    this.api.updateAccount({
      fullName: this.form.fullName.trim(),
      phone: this.form.phone.trim(),
      currentPassword: this.form.newPassword ? this.form.currentPassword : null,
      newPassword: this.form.newPassword || null,
    }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: account => {
        this.account.set(account);
        this.auth.updateAccountState(account);
        this.form.fullName = account.fullName;
        this.form.phone = account.phone ?? '';
        this.form.currentPassword = '';
        this.form.newPassword = '';
        this.form.confirmPassword = '';
        this.alerts.success('Đã cập nhật hồ sơ');
      },
      error: error => this.saveError.set(this.api.errorMessage(error)),
    });
  }
}
