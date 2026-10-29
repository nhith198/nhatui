import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { Api } from '../core/api';
import { Icon } from '../core/icon';
import { ContractStatus, Occupant, Room, Tenancy, contractLabels } from '../core/models';
import { Alerts } from '../core/alerts';

@Component({
  selector: 'app-tenancies', imports: [FormsModule, CurrencyPipe, DatePipe, Icon],
  template: `
    <div class="page-head"><div><h2>Khách thuê & hợp đồng</h2><p class="mute">{{ tenants().length }} hợp đồng · {{ activeCount() }} đang thuê</p></div></div>
    <div class="toolbar">
      <input type="search" aria-label="Tìm khách thuê" placeholder="Tên, điện thoại, phòng…" [ngModel]="search()" (ngModelChange)="search.set($event)">
      <select aria-label="Trạng thái hợp đồng" [ngModel]="status()" (ngModelChange)="status.set($event)">
        <option value="">Tất cả hợp đồng</option>
        @for (state of states; track state) { <option [value]="state">{{ labels[state] }}</option> }
      </select>
      <button class="p" (click)="openOccupant()">Thêm người ở cùng</button>
    </div>
    @if (error()) { <div class="error-banner" role="alert">{{ error() }} <button (click)="reload()">Thử lại</button></div> }
    @if (loading()) { <div class="empty" role="status">Đang tải khách thuê…</div> }
    @else {
      <div class="tbl"><table>
        <thead><tr><th>Khách thuê</th><th>Nhà / Phòng</th><th>Thời hạn</th><th>Hợp đồng</th><th class="n">Tiền cọc</th><th><span class="sr-only">Thao tác</span></th></tr></thead>
        <tbody>
          @for (tenant of filtered(); track tenant.id) {
            <tr><td><b>{{ tenant.name }}</b><div class="mute">{{ tenant.phone || 'Chưa có điện thoại' }}</div></td>
              <td>{{ tenant.roomName }}<div class="mute">{{ tenant.propertyName }}</div></td>
              <td>Bắt đầu: {{ tenant.startDate | date:'dd/MM/yyyy' }}<div class="mute">Hết hạn: {{ tenant.expectedEndDate ? (tenant.expectedEndDate | date:'dd/MM/yyyy') : 'Không thời hạn' }}</div></td>
              <td><span class="tag" [class.ok]="tenant.status === 'Active'" [class.warn]="tenant.status === 'Expiring' || tenant.status === 'Upcoming'" [class.bad]="tenant.status === 'Expired'">{{ labels[tenant.status] }}</span>
                @if (tenant.endDate) { <div class="mute">Trả {{ tenant.endDate | date:'dd/MM/yyyy' }}</div> }
              </td>
              <td class="n">{{ tenant.deposit | currency:'VND':'symbol':'1.0-0' }}</td>
              <td><div class="row nowrap"><button class="icon-button" title="Sửa hồ sơ và hợp đồng" aria-label="Sửa hồ sơ và hợp đồng" (click)="edit(tenant)"><app-icon name="edit" /></button>
                @if (!tenant.endDate) { <button class="s" [disabled]="busy()" (click)="checkout(tenant)">Trả phòng</button> }
              </div></td></tr>
          } @empty { <tr><td colspan="6" class="empty">Không có khách thuê phù hợp.</td></tr> }
        </tbody>
      </table></div>
      @if (occupants().length) {
        <h3>Người ở cùng (không đứng tên hợp đồng)</h3>
        <div class="tbl"><table>
          <thead><tr><th>Họ và tên</th><th>Nhà / Phòng</th><th>Điện thoại</th><th>CCCD</th><th><span class="sr-only">Thao tác</span></th></tr></thead>
          <tbody>
            @for (occupant of occupants(); track occupant.id) {
              <tr><td><b>{{ occupant.name }}</b><div class="mute">{{ occupant.gender }}</div></td>
                <td>{{ occupant.roomName }}<div class="mute">{{ occupant.propertyName }}</div></td>
                <td>{{ occupant.phone || 'Chưa có điện thoại' }}</td>
                <td>{{ occupant.identityNumber || '—' }}</td>
                <td><div class="row nowrap"><button class="s" [disabled]="busy()" (click)="editOccupant(occupant)">Sửa</button>
                  <button class="s d" [disabled]="busy()" title="Xóa người ở cùng" aria-label="Xóa người ở cùng" (click)="removeOccupant(occupant)"><app-icon name="trash" /></button></div></td></tr>
            }
          </tbody>
        </table></div>
      }
    }
    <dialog #editor class="modal" aria-label="Hồ sơ khách thuê" (cancel)="busy() && $event.preventDefault()">
      <form #profileForm="ngForm" (ngSubmit)="profileForm.valid && save()">
        <div class="modal-head"><div><h2>Hồ sơ khách thuê</h2><p class="mute">{{ editing()?.propertyName }} / {{ editing()?.roomName }}</p></div>
          <button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="close()"><app-icon name="close" /></button></div>
        <div class="form-grid">
          <label>Họ và tên<input name="name" [(ngModel)]="form.name" required maxlength="200"></label>
          <label>Điện thoại<input name="phone" type="tel" [(ngModel)]="form.phone" maxlength="30"></label>
          <label>CCCD / Hộ chiếu<input name="identity" [(ngModel)]="form.identityNumber" maxlength="30"></label>
          <label>Giới tính<select name="gender" [(ngModel)]="form.gender"><option value="">-- Chọn --</option><option value="Nam">Nam</option><option value="Nữ">Nữ</option></select></label>
          <label class="span-2">Địa chỉ thường trú<input name="address" [(ngModel)]="form.address" maxlength="500"></label>
          <label class="check-label span-2"><input name="temporaryResidence" type="checkbox" [(ngModel)]="form.temporaryResidenceRegistered">Đã đăng ký tạm trú</label>
          <label>Ngày hết hạn hợp đồng<input name="expectedEnd" type="date" [(ngModel)]="form.expectedEndDate" [min]="editing()?.startDate || ''"></label>
          <label class="span-2">Ghi chú<textarea name="notes" [(ngModel)]="form.notes" rows="3" maxlength="2000"></textarea></label>
        </div>
        @if (saveError()) { <p class="err" role="alert">{{ saveError() }}</p> }
        <div class="acts"><button type="button" (click)="close()">Hủy</button><button type="submit" class="p" [disabled]="busy() || profileForm.invalid"><app-icon name="save" />{{ busy() ? 'Đang lưu…' : 'Lưu hồ sơ' }}</button></div>
      </form>
    </dialog>
    <dialog #occupantEditor class="modal" aria-label="Người ở cùng" (cancel)="busy() && $event.preventDefault()">
      <form #occupantForm="ngForm" (ngSubmit)="occupantForm.valid && saveOccupant()">
        <div class="modal-head"><div><h2>{{ editingOccupant() ? 'Sửa người ở cùng' : 'Thêm người ở cùng' }}</h2><p class="mute">Người không đứng tên trên hợp đồng</p></div>
          <button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="closeOccupant()"><app-icon name="close" /></button></div>
        <div class="form-grid">
          <label class="span-2">Phòng thuê<select name="occRoom" [(ngModel)]="occupantFields.roomId" required><option value="">-- Chọn phòng --</option>@for (room of rooms(); track room.id) { <option [value]="room.id">{{ room.propertyName }} / {{ room.name }}</option> }</select></label>
          <label>Họ và tên<input name="occName" [(ngModel)]="occupantFields.name" required maxlength="200"></label>
          <label>Điện thoại<input name="occPhone" type="tel" [(ngModel)]="occupantFields.phone" maxlength="30"></label>
          <label>CCCD / Hộ chiếu<input name="occIdentity" [(ngModel)]="occupantFields.identityNumber" maxlength="30"></label>
          <label>Giới tính<select name="occGender" [(ngModel)]="occupantFields.gender"><option value="">-- Chọn --</option><option value="Nam">Nam</option><option value="Nữ">Nữ</option></select></label>
          <label class="span-2">Địa chỉ thường trú<input name="occAddress" [(ngModel)]="occupantFields.address" maxlength="500"></label>
          <label class="check-label span-2"><input name="occTemporaryResidence" type="checkbox" [(ngModel)]="occupantFields.temporaryResidenceRegistered">Đã đăng ký tạm trú</label>
          <label class="span-2">Ghi chú<textarea name="occNotes" [(ngModel)]="occupantFields.notes" rows="2" maxlength="2000"></textarea></label>
        </div>
        @if (occupantError()) { <p class="err" role="alert">{{ occupantError() }}</p> }
        <div class="acts"><button type="button" (click)="closeOccupant()">Hủy</button><button type="submit" class="p" [disabled]="busy() || occupantForm.invalid">{{ busy() ? 'Đang lưu…' : 'Lưu' }}</button></div>
      </form>
    </dialog>
  `,
})
export class Tenancies {
  api = inject(Api);
  private alerts = inject(Alerts);
  private editor = viewChild.required<ElementRef<HTMLDialogElement>>('editor');
  private revision = signal(0);
  tenants = signal<Tenancy[]>([]);
  search = signal('');
  status = signal('');
  loading = signal(true);
  busy = signal(false);
  error = signal('');
  saveError = signal('');
  editing = signal<Tenancy | null>(null);
  occupants = signal<Occupant[]>([]);
  rooms = signal<Room[]>([]);
  editingOccupant = signal<Occupant | null>(null);
  occupantError = signal('');
  private occupantEditor = viewChild.required<ElementRef<HTMLDialogElement>>('occupantEditor');
  occupantFields = { roomId: '', name: '', phone: '', identityNumber: '', gender: '', address: '', notes: '', temporaryResidenceRegistered: false };
  labels = contractLabels;
  states = Object.keys(contractLabels) as ContractStatus[];
  form = { name: '', phone: '', identityNumber: '', gender: '', address: '', notes: '', expectedEndDate: '', temporaryResidenceRegistered: false };
  activeCount = computed(() => this.tenants().filter(tenant => !tenant.endDate).length);
  filtered = computed(() => {
    const text = this.search().trim().toLocaleLowerCase('vi');
    return this.tenants().filter(tenant => (!this.status() || tenant.status === this.status()) &&
      `${tenant.name} ${tenant.phone} ${tenant.roomName} ${tenant.propertyName}`.toLocaleLowerCase('vi').includes(text));
  });

  constructor() {
    effect(cleanup => {
      this.api.pid(); this.revision(); this.loading.set(true); this.error.set(''); this.tenants.set([]); this.occupants.set([]);
      const subs = [
        this.api.tenancies().subscribe({ next: rows => { this.tenants.set(rows); this.loading.set(false); }, error: err => { this.error.set(this.api.errorMessage(err)); this.loading.set(false); } }),
        this.api.occupants().subscribe({ next: rows => this.occupants.set(rows), error: err => this.api.say(this.api.errorMessage(err)) }),
        this.api.rooms().subscribe({ next: rows => this.rooms.set(rows), error: err => this.api.say(this.api.errorMessage(err)) }),
      ];
      cleanup(() => subs.forEach(sub => sub.unsubscribe()));
    });
  }
  reload() { this.revision.update(value => value + 1); }
  edit(tenant: Tenancy) {
    this.editing.set(tenant); this.saveError.set('');
    this.form = { name: tenant.name, phone: tenant.phone, identityNumber: tenant.identityNumber, gender: tenant.gender ?? '',
      address: tenant.address, notes: tenant.notes, expectedEndDate: tenant.expectedEndDate ?? '', temporaryResidenceRegistered: tenant.temporaryResidenceRegistered };
    this.editor().nativeElement.showModal();
  }
  close() { if (this.busy()) return; this.editor().nativeElement.close(); this.editing.set(null); }
  save() {
    const tenant = this.editing();
    if (!tenant || this.busy() || !this.form.name.trim()) return;
    if (this.form.expectedEndDate && this.form.expectedEndDate < tenant.startDate) { this.saveError.set('Ngày hết hạn phải từ ngày bắt đầu thuê'); return; }
    this.busy.set(true); this.saveError.set('');
    this.api.updateTenant(tenant, { ...this.form, gender: this.form.gender || null, expectedEndDate: this.form.expectedEndDate || null }).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.editor().nativeElement.close(); this.editing.set(null); this.reload(); this.alerts.success('Đã cập nhật hồ sơ và hợp đồng'); },
      error: err => this.saveError.set(this.api.errorMessage(err)),
    });
  }
  openOccupant() {
    this.editingOccupant.set(null); this.occupantError.set('');
    this.occupantFields = { roomId: '', name: '', phone: '', identityNumber: '', gender: '', address: '', notes: '', temporaryResidenceRegistered: false };
    this.occupantEditor().nativeElement.showModal();
  }
  editOccupant(occupant: Occupant) {
    this.editingOccupant.set(occupant); this.occupantError.set('');
    this.occupantFields = { roomId: occupant.roomId, name: occupant.name, phone: occupant.phone, identityNumber: occupant.identityNumber,
      gender: occupant.gender ?? '', address: occupant.address, notes: occupant.notes, temporaryResidenceRegistered: occupant.temporaryResidenceRegistered };
    this.occupantEditor().nativeElement.showModal();
  }
  closeOccupant() { if (this.busy()) return; this.occupantEditor().nativeElement.close(); this.editingOccupant.set(null); }
  saveOccupant() {
    if (this.busy() || !this.occupantFields.name.trim() || !this.occupantFields.roomId) return;
    const id = this.editingOccupant()?.id;
    this.busy.set(true); this.occupantError.set('');
    this.api.saveOccupant({ ...this.occupantFields, gender: this.occupantFields.gender || null }, id).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.occupantEditor().nativeElement.close(); this.editingOccupant.set(null); this.reload(); this.alerts.success(id ? 'Đã cập nhật người ở cùng' : 'Đã thêm người ở cùng'); },
      error: err => this.occupantError.set(this.api.errorMessage(err)),
    });
  }
  async removeOccupant(occupant: Occupant) {
    if (!await this.alerts.confirm('Xóa người ở cùng?', `${occupant.name} sẽ bị xóa khỏi ${occupant.roomName}.`, 'Xóa')) return;
    this.busy.set(true);
    this.api.deleteOccupant(occupant.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.reload(); this.alerts.success('Đã xóa người ở cùng'); }, error: err => this.api.say(this.api.errorMessage(err)) });
  }
  async checkout(tenant: Tenancy) {
    if (!await this.alerts.confirm('Xác nhận trả phòng?', `${tenant.name} sẽ trả phòng ${tenant.roomName}.`, 'Trả phòng')) return;
    this.busy.set(true);
    this.api.checkout(tenant.roomId).pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.reload(); this.api.loadProperties(); this.alerts.success('Đã trả phòng'); }, error: err => this.api.say(this.api.errorMessage(err)) });
  }
}
