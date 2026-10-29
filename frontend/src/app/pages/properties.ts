import { CurrencyPipe } from '@angular/common';
import { Component, effect, ElementRef, inject, OnDestroy, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Property } from '../core/models';
import { VndInput } from '../core/vnd-input';
import { Icon } from '../core/icon';
import { Alerts } from '../core/alerts';
import { BANK_NAMES } from '../core/vietqr';

@Component({
  selector: 'app-properties',
  imports: [FormsModule, CurrencyPipe, VndInput, Icon],
  template: `
    @if (!auth.isEmployee()) {
      <div class="bar"><button class="p" (click)="add()">Thêm nhà</button></div>
    }
    <div class="grid">
      @for (p of api.properties(); track p.id) {
        <div class="card room">
          <b>{{ p.name }}</b>
          <div class="mute">{{ p.address }}</div>
          <div>{{ p.occupiedCount }}/{{ p.roomCount }} phòng đang thuê</div>
          <div class="mute">Điện {{ p.electricRate | currency: 'VND':'symbol':'1.0-0' }}/kWh</div>
          <div class="mute">Nước theo khối {{ p.waterRate | currency: 'VND':'symbol':'1.0-0' }}/m³ · Theo người {{ p.waterRatePerPerson | currency: 'VND':'symbol':'1.0-0' }}/người/tháng</div>
          <div class="mute">Dịch vụ mặc định {{ p.serviceAmountPerMonth | currency: 'VND':'symbol':'1.0-0' }}/phòng/tháng</div>
          @if (p.bankName || p.bankAccountNumber) { <div class="mute">{{ p.bankName }} · {{ p.bankAccountNumber }} · {{ p.bankAccountName }}</div> }
          @if (cardQr()[p.id]; as url) { <img style="max-width:120px;display:block;margin:4px 0" [src]="url" alt="Mã QR nhận tiền"> }
          @if (p.canManage) { <div class="row"><button class="s" (click)="edit(p)">Sửa</button><button class="s d" title="Xóa nhà" aria-label="Xóa nhà" (click)="remove(p)"><app-icon name="trash" /></button></div> }
        </div>
      } @empty {
        <div class="card empty">Chưa có nhà nào. Bấm Thêm nhà để bắt đầu.</div>
      }
    </div>
    <dialog #editor class="modal compact-modal" aria-label="Thông tin nhà">
      <form #propertyForm="ngForm" (ngSubmit)="propertyForm.valid && save()">
        <div class="modal-head"><h2>{{ editId ? 'Sửa nhà' : 'Thêm nhà' }}</h2><button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="close()"><app-icon name="close" /></button></div>
        <div class="form-grid">
          <label class="span-2">Tên nhà<input name="name" [(ngModel)]="f.name" required maxlength="200"></label>
          <label class="span-2">Địa chỉ<input name="address" [(ngModel)]="f.address" maxlength="500"></label>
        </div>
        <fieldset class="assignment-field"><legend>Đơn giá và chi phí hàng tháng</legend>
          <div class="form-grid">
            <label>Giá điện (₫/kWh)<input vndInput inputmode="decimal" name="electricRate" [(ngModel)]="f.electricRate" min="0" max="1000000" required></label>
            <label>Giá nước theo khối (₫/m³)<input vndInput inputmode="decimal" name="waterRate" [(ngModel)]="f.waterRate" min="0" max="1000000" required></label>
            <label>Giá nước theo đầu người (₫/người/tháng)<input vndInput inputmode="decimal" name="waterRatePerPerson" [(ngModel)]="f.waterRatePerPerson" min="0" max="1000000" required></label>
            <label>Chi phí dịch vụ/tháng (₫)<input vndInput inputmode="decimal" name="serviceAmountPerMonth" [(ngModel)]="f.serviceAmountPerMonth" min="0" max="1000000000" required>
              <small class="mute">Mặc định cho phòng mới; từng phòng có thể chỉnh riêng.</small>
            </label>
          </div>
        </fieldset>
        <fieldset class="assignment-field"><legend>Tài khoản nhận thanh toán</legend>
          <div class="form-grid">
            <label>Tên ngân hàng
              <select name="bankName" [(ngModel)]="f.bankName">
                <option value="">-- Chọn ngân hàng --</option>
                @for (b of bankOptions(); track b) { <option [value]="b">{{ b }}</option> }
              </select>
            </label>
            <label>Số tài khoản<input name="bankAccountNumber" [(ngModel)]="f.bankAccountNumber" maxlength="50"></label>
            <label class="span-2">Tên người nhận<input name="bankAccountName" [(ngModel)]="f.bankAccountName" maxlength="200"></label>
          </div>
        </fieldset>
        <fieldset class="assignment-field"><legend>Mã QR nhận tiền</legend>
          @if (qrPreview) { <img style="max-width:160px;display:block;margin:8px 0" [src]="qrPreview" alt="Mã QR nhận tiền"> }
          <input type="file" accept="image/png,image/jpeg" (change)="pickQr($event)">
          <small class="mute">PNG hoặc JPG, tối đa 2 MB.</small>
          @if (editId && hasQr) { <div class="acts"><button type="button" class="s d" (click)="removeQr()">Xóa QR</button></div> }
        </fieldset>
        <div class="acts"><button type="button" (click)="close()">Hủy</button><button class="p" type="submit" [disabled]="propertyForm.invalid">Lưu</button></div>
      </form>
    </dialog>
  `,
})
export class Properties implements OnDestroy {
  api = inject(Api);
  auth = inject(Auth);
  private alerts = inject(Alerts);
  private editor = viewChild.required<ElementRef<HTMLDialogElement>>('editor');
  cardQr = signal<Record<string, string>>({});
  bankOptions = () => this.f.bankName && !BANK_NAMES.includes(this.f.bankName) ? [...BANK_NAMES, this.f.bankName] : BANK_NAMES;
  private loadedQr = new Set<string>();
  editId = '';
  qrFile: File | null = null;
  qrPreview = '';
  hasQr = false;
  f = { name: '', address: '', electricRate: 3500, waterRate: 18000, waterRatePerPerson: 0,
    bankName: '', bankAccountNumber: '', bankAccountName: '', serviceAmountPerMonth: 0 };

  constructor() {
    effect(() => {
      const list = this.api.properties();
      untracked(() => list.filter(p => p.hasQrCode && !this.loadedQr.has(p.id)).forEach(p => this.loadCardQr(p.id)));
    });
  }

  ngOnDestroy() { this.resetCardQr(); }

  add() {
    this.resetQr();
    this.editId = '';
    this.f = { name: '', address: '', electricRate: 3500, waterRate: 18000, waterRatePerPerson: 0,
      bankName: '', bankAccountNumber: '', bankAccountName: '', serviceAmountPerMonth: 0 };
    this.editor().nativeElement.showModal();
  }
  edit(p: Property) {
    this.resetQr();
    this.hasQr = p.hasQrCode;
    if (p.hasQrCode) this.api.propertyQr(p.id).subscribe(blob => this.qrPreview = URL.createObjectURL(blob));
    this.editId = p.id;
    this.f = { name: p.name, address: p.address, electricRate: p.electricRate, waterRate: p.waterRate,
      waterRatePerPerson: p.waterRatePerPerson, bankName: p.bankName, bankAccountNumber: p.bankAccountNumber,
      bankAccountName: p.bankAccountName, serviceAmountPerMonth: p.serviceAmountPerMonth };
    this.editor().nativeElement.showModal();
  }
  close() { this.editor().nativeElement.close(); this.editId = ''; this.resetQr(); }

  save() {
    if (!this.f.name.trim()) return this.api.say('Vui lòng nhập tên nhà');
    const message = this.editId ? 'Đã cập nhật nhà' : 'Đã thêm nhà';
    this.api.saveProperty(this.f, this.editId || undefined).subscribe({
      next: res => this.uploadQr(this.editId || (res as { id: string }).id, message),
      error: e => this.api.say(this.api.errorMessage(e)),
    });
  }

  pickQr(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 2_097_152) return this.api.say('Ảnh QR phải là PNG hoặc JPG, tối đa 2 MB');
    this.qrFile = file;
    this.qrPreview = URL.createObjectURL(file);
  }

  removeQr() {
    this.api.deletePropertyQr(this.editId).subscribe({
      next: () => { this.resetQr(); this.resetCardQr(); this.api.loadProperties(); this.alerts.success('Đã xóa QR'); },
      error: e => this.api.say(this.api.errorMessage(e)),
    });
  }

  private uploadQr(propertyId: string, message: string) {
    if (!this.qrFile) return this.finishSave(message);
    this.api.uploadPropertyQr(propertyId, this.qrFile).subscribe({
      next: () => this.finishSave(message),
      error: e => { this.api.say(this.api.errorMessage(e)); this.finishSave(message); },
    });
  }

  private finishSave(message: string) {
    this.close(); this.resetCardQr(); this.api.loadProperties(); this.alerts.success(message);
  }

  private loadCardQr(id: string) {
    this.loadedQr.add(id);
    this.api.propertyQr(id).subscribe(blob => this.cardQr.update(m => ({ ...m, [id]: URL.createObjectURL(blob) })));
  }

  private resetCardQr() {
    Object.values(this.cardQr()).forEach(url => URL.revokeObjectURL(url));
    this.cardQr.set({});
    this.loadedQr.clear();
  }

  private resetQr() {
    this.qrFile = null;
    this.hasQr = false;
    if (this.qrPreview) URL.revokeObjectURL(this.qrPreview);
    this.qrPreview = '';
  }

  async remove(p: Property) {
    if (!await this.alerts.confirm('Xóa nhà?', `Bạn có chắc muốn xóa nhà ${p.name} và dữ liệu liên quan?`, 'Xóa', true)) return;
    this.api.deleteProperty(p.id).subscribe({
      next: () => { if (this.api.pid() === p.id) this.api.pid.set(''); this.api.loadProperties(); this.alerts.success('Đã xóa nhà'); },
      error: e => this.api.say(this.api.errorMessage(e)),
    });
  }
}
