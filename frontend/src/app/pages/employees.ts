import { Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Icon } from '../core/icon';
import { Employee, employeePageOptions } from '../core/models';
import { Alerts } from '../core/alerts';

@Component({
  selector: 'app-employees', imports: [FormsModule, Icon],
  template: `
    <div class="page-head"><div><h2>Nhân viên</h2><p class="mute">{{ employees().length }} tài khoản</p></div>
      @if (!auth.isEmployee()) { <button class="p" (click)="edit()"><app-icon name="plus" />Thêm nhân viên</button> }
    </div>
    @if (auth.isEmployee()) { <p class="empty">Bạn không có quyền quản lý nhân viên.</p> }
    @else {
      @if (error()) { <div class="error-banner" role="alert">{{ error() }} <button (click)="reload()">Thử lại</button></div> }
      @if (loading()) { <p class="empty" role="status">Đang tải nhân viên…</p> }
      @else {
        <div class="tbl"><table><thead><tr><th>Nhân viên</th><th>Nhà phụ trách</th><th>Trang được phép</th><th>Trạng thái</th><th><span class="sr-only">Thao tác</span></th></tr></thead><tbody>
          @for (employee of employees(); track employee.id) {
            <tr><td><b>{{ employee.fullName }}</b><div class="mute">{{ employee.phone || 'Chưa có số điện thoại' }}</div></td>
              <td><div class="assignment-list">@for (id of employee.propertyIds; track id) { <span class="tag">{{ propertyName(id) }}</span> } @empty { <span class="mute">Chưa phân công</span> }</div></td>
              <td><div class="assignment-list">@for (page of employee.pagePermissions; track page) { <span class="tag">{{ pageLabel(page) }}</span> } @empty { <span class="mute">Chưa cấp quyền</span> }</div></td>
              <td><span class="tag" [class.ok]="!employee.isDisabled" [class.bad]="employee.isDisabled">{{ employee.isDisabled ? 'Đã khóa' : 'Hoạt động' }}</span></td>
              <td class="n"><button class="icon-button" title="Sửa nhân viên và phân quyền" aria-label="Sửa nhân viên và phân quyền" (click)="edit(employee)"><app-icon name="edit" /></button> <button class="s d" title="Xóa nhân viên" aria-label="Xóa nhân viên" (click)="remove(employee)"><app-icon name="trash" /></button></td></tr>
          } @empty { <tr><td colspan="5" class="empty">Chưa có nhân viên.</td></tr> }
        </tbody></table></div>
      }
    }
    <dialog #editor class="modal" aria-label="Thông tin nhân viên" (cancel)="busy() && $event.preventDefault()">
      <form #employeeForm="ngForm" (ngSubmit)="employeeForm.valid && save()">
        <div class="modal-head"><h2>{{ editId() ? 'Chỉnh sửa nhân viên' : 'Thêm nhân viên' }}</h2><button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="close()"><app-icon name="close" /></button></div>
        <div class="form-grid">
          <label>Họ và tên<input name="name" [(ngModel)]="form.fullName" required maxlength="200"></label>
          <label>Số điện thoại đăng nhập<input name="phone" type="tel" inputmode="tel" autocomplete="tel" [(ngModel)]="form.phone" required maxlength="30"></label>
          <label class="span-2">{{ editId() ? 'Mật khẩu mới (tùy chọn)' : 'Mật khẩu' }}<input name="password" type="password" [(ngModel)]="form.password" [required]="!editId()" minlength="8" maxlength="128" autocomplete="new-password"></label>
        </div>
        <fieldset class="assignment-field"><legend>Nhà được phân công</legend>
          @for (property of api.properties(); track property.id) {
            @if (property.canManage) { <label class="check-label"><input type="checkbox" [checked]="form.propertyIds.includes(property.id)" (change)="toggleProperty(property.id)">{{ property.name }}</label> }
          }
        </fieldset>
        <fieldset class="assignment-field"><legend>Trang được phép truy cập</legend>
          @for (page of pageOptions; track page.key) {
            <label class="check-label"><input type="checkbox" [checked]="form.pagePermissions.includes(page.key)" (change)="togglePage(page.key)">{{ page.label }}</label>
          }
        </fieldset>
        <label class="check-label"><input type="checkbox" name="disabled" [(ngModel)]="form.isDisabled">Khóa tài khoản</label>
        @if (saveError()) { <p class="err" role="alert">{{ saveError() }}</p> }
        <div class="acts"><button type="button" (click)="close()">Hủy</button><button class="p" type="submit" [disabled]="busy() || employeeForm.invalid"><app-icon name="save" />{{ busy() ? 'Đang lưu…' : 'Lưu nhân viên' }}</button></div>
      </form>
    </dialog>
  `,
})
export class Employees {
  api = inject(Api);
  auth = inject(Auth);
  private alerts = inject(Alerts);
  private editor = viewChild.required<ElementRef<HTMLDialogElement>>('editor');
  private revision = signal(0);
  employees = signal<Employee[]>([]);
  editId = signal<string | null>(null);
  loading = signal(true);
  busy = signal(false);
  error = signal('');
  saveError = signal('');
  form = { fullName: '', phone: '', password: '', isDisabled: false, propertyIds: [] as string[], pagePermissions: [] as string[] };
  pageOptions = employeePageOptions;
  constructor() {
    effect(cleanup => {
      this.revision(); if (this.auth.isEmployee()) return;
      this.loading.set(true); this.error.set(''); this.employees.set([]);
      const sub = this.api.employees().subscribe({ next: rows => { this.employees.set(rows); this.loading.set(false); }, error: err => { this.error.set(this.api.errorMessage(err)); this.loading.set(false); } });
      cleanup(() => sub.unsubscribe());
    });
  }
  reload() { this.revision.update(value => value + 1); }
  propertyName(id: string) { return this.api.properties().find(property => property.id === id)?.name ?? 'Nhà không còn khả dụng'; }
  toggleProperty(id: string) { this.form.propertyIds = this.form.propertyIds.includes(id) ? this.form.propertyIds.filter(value => value !== id) : [...this.form.propertyIds, id]; }
  togglePage(page: string) { this.form.pagePermissions = this.form.pagePermissions.includes(page) ? this.form.pagePermissions.filter(value => value !== page) : [...this.form.pagePermissions, page]; }
  pageLabel(key: string) { return this.pageOptions.find(page => page.key === key)?.label ?? key; }
  edit(employee?: Employee) {
    this.editId.set(employee?.id ?? null); this.saveError.set('');
    this.form = { fullName: employee?.fullName ?? '', phone: employee?.phone ?? '', password: '', isDisabled: employee?.isDisabled ?? false,
      propertyIds: [...employee?.propertyIds ?? []], pagePermissions: [...employee?.pagePermissions ?? []] };
    this.editor().nativeElement.showModal();
  }
  close() { if (this.busy()) return; this.editor().nativeElement.close(); this.editId.set(null); this.form.password = ''; }
  async remove(employee: Employee) {
    if (!await this.alerts.confirm('Xóa nhân viên?', employee.fullName, 'Xóa', true)) return;
    this.api.deleteEmployee(employee.id).subscribe({
      next: () => { this.reload(); this.alerts.success('Đã xóa nhân viên'); },
      error: err => this.api.say(this.api.errorMessage(err)),
    });
  }
  save() {
    if (this.busy() || !this.form.fullName.trim()) return;
    this.busy.set(true); this.saveError.set('');
    this.api.saveEmployee({ ...this.form, password: this.form.password || null }, this.editId() ?? undefined).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.editor().nativeElement.close(); this.form.password = ''; this.reload(); this.alerts.success('Đã lưu nhân viên và phân quyền'); },
      error: err => this.saveError.set(this.api.errorMessage(err)),
    });
  }
}
