import { CurrencyPipe } from '@angular/common';
import { Component, ElementRef, OnDestroy, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { concat, finalize, forkJoin, of, switchMap, toArray } from 'rxjs';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Icon } from '../core/icon';
import { Room, RoomImage } from '../core/models';
import { VndInput } from '../core/vnd-input';
import { Alerts } from '../core/alerts';
import { MonthYearPipe } from '../core/month-year';

type Panel = 'room' | 'rent' | 'reserve' | 'cancel' | 'bill' | 'maintenance' | null;

@Component({
  selector: 'app-rooms',
  imports: [FormsModule, CurrencyPipe, Icon, VndInput, MonthYearPipe],
  template: `
    <div class="page-head"><div><h2>Phòng</h2><p class="mute">{{ rooms().length }} phòng · {{ vacantCount() }} sẵn sàng cho thuê</p></div>
      @if (!auth.isEmployee()) { <button class="p" (click)="openRoom()"><app-icon name="plus" />Thêm phòng</button> }
    </div>
    <div class="toolbar"><input type="search" aria-label="Tìm phòng" placeholder="Tên phòng, khách thuê…" [ngModel]="search()" (ngModelChange)="search.set($event)">
      <div class="segments" role="group" aria-label="Trạng thái phòng">
        @for (state of states; track state.value) { <button [attr.aria-pressed]="status() === state.value" (click)="status.set(state.value)">{{ state.label }}</button> }
      </div>
    </div>
    <dialog #editor class="modal compact-modal" aria-label="Thao tác phòng" (cancel)="busy() && $event.preventDefault()" (close)="panel.set(null)">
    @switch (panel()) {
      @case ('room') {
        <form #roomEditor="ngForm" (ngSubmit)="roomEditor.valid && saveRoom()">
          <div class="modal-head"><h2>{{ editRoomId ? 'Sửa phòng' : 'Thêm phòng' }}</h2><button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="closeEditor()"><app-icon name="close" /></button></div>
          <fieldset class="assignment-field"><legend>Thông tin phòng</legend>
            <div class="form-grid">
              <label class="span-2">Nhà
                <select name="property" [(ngModel)]="roomForm.propertyId" (ngModelChange)="selectProperty($event)" [disabled]="!!editRoomId" required>
                  @for (p of api.properties(); track p.id) { @if (p.canManage) { <option [value]="p.id">{{ p.name }}</option> } }
                </select>
              </label>
              <label>Tên phòng<input name="name" [(ngModel)]="roomForm.name" required maxlength="100"></label>
              <label>Giá thuê/tháng<input vndInput inputmode="decimal" name="price" [(ngModel)]="roomForm.price" required min="0"></label>
              <label>Số người ở<input name="occupantCount" type="number" [(ngModel)]="roomForm.occupantCount" required min="1" max="100"></label>
              <label>Chi phí dịch vụ/tháng<input vndInput inputmode="decimal" name="serviceAmount" [(ngModel)]="roomForm.serviceAmount" required min="0"></label>
              <label>Giá điện riêng/kWh (để trống = theo nhà)<input name="electricRate" type="number" min="0" [(ngModel)]="roomForm.electricRate"></label>
            </div>
          </fieldset>
          <fieldset class="assignment-field"><legend>Tiện ích và chỉ số</legend>
            <label class="check-label"><input name="waterByOccupant" type="checkbox" [(ngModel)]="roomForm.waterByOccupant">Tính tiền nước theo đầu người</label>
            <label>Số điện hiện tại<input name="electric" type="number" [(ngModel)]="roomForm.lastElectric" required min="0"></label>
            <label>Số nước hiện tại<input name="water" type="number" [(ngModel)]="roomForm.lastWater" required min="0"></label>
          </fieldset>
          <div class="acts"><button type="button" (click)="closeEditor()">Hủy</button><button type="submit" class="p" [disabled]="busy() || roomEditor.invalid"><app-icon name="save" />Lưu phòng</button></div>
        </form>
      }
      @case ('rent') {
        <form #rentEditor="ngForm" (ngSubmit)="rentEditor.valid && saveRent()">
          <h2 class="first">Cho thuê phòng {{ target?.name }}</h2>
          <label>Tên khách thuê<input name="tenant" [(ngModel)]="rentForm.tenantName" required maxlength="200"></label>
          <label>Số điện thoại<input name="phone" type="tel" [(ngModel)]="rentForm.phone" maxlength="30"></label>
          <label>Ngày vào ở<input name="start" type="date" [(ngModel)]="rentForm.startDate" (ngModelChange)="updateRentEndDate()" required></label>
          <label>Số tháng thuê<input name="rentMonths" type="number" min="1" max="120" step="1" [(ngModel)]="rentMonths" (ngModelChange)="updateRentEndDate()"></label>
          <label>Ngày hết hạn hợp đồng<input name="end" type="date" [(ngModel)]="rentForm.expectedEndDate" (ngModelChange)="rentMonths = null" [min]="rentForm.startDate"></label>
          <label>Tiền cọc<input vndInput inputmode="decimal" name="deposit" [(ngModel)]="rentForm.deposit" required min="0"></label>
          <div class="acts"><button type="button" (click)="closeEditor()">Hủy</button><button class="p" type="submit" [disabled]="busy() || rentEditor.invalid"><app-icon name="save" />Nhận khách</button></div>
        </form>
      }
      @case ('reserve') {
        <form #reserveEditor="ngForm" (ngSubmit)="reserveEditor.valid && saveReserve()">
          <h2 class="first">Cọc giữ phòng {{ target?.name }}</h2>
          <p class="mute">Phòng sẽ không cho khách khác thuê cho đến khi nhận khách hoặc hủy giữ cọc.</p>
          <label>Tên khách đặt cọc<input name="reserveTenant" [(ngModel)]="reserveForm.tenantName" required maxlength="200"></label>
          <label>Số điện thoại<input name="reservePhone" type="tel" [(ngModel)]="reserveForm.phone" maxlength="30"></label>
          <label>Số tiền cọc<input vndInput inputmode="decimal" name="reserveDeposit" [(ngModel)]="reserveForm.deposit" required min="0"></label>
          <label>Giữ phòng đến ngày<input name="reserveUntil" type="date" [(ngModel)]="reserveForm.holdUntil" [min]="today()" required></label>
          <div class="acts"><button type="button" (click)="closeEditor()">Hủy</button><button class="p" type="submit" [disabled]="busy() || reserveEditor.invalid"><app-icon name="save" />Xác nhận cọc</button></div>
        </form>
      }
      @case ('cancel') {
        <form #cancelEditor="ngForm" (ngSubmit)="cancelEditor.valid && saveCancelReservation()">
          <h2 class="first">Hủy giữ cọc phòng {{ target?.name }}</h2>
          <p class="mute">Tiền cọc: {{ target?.reservedDeposit | currency:'VND':'symbol':'1.0-0' }}. Nhập số tiền hoàn trả cho khách.</p>
          <label>Số tiền hoàn trả<input vndInput inputmode="decimal" name="refundAmount" [(ngModel)]="cancelForm.refund" required min="0" [max]="target?.reservedDeposit ?? 0"></label>
          <div class="acts"><button type="button" (click)="closeEditor()">Đóng</button><button class="p" type="submit" [disabled]="busy() || cancelEditor.invalid"><app-icon name="save" />Xác nhận hủy giữ cọc</button></div>
        </form>
      }
      @case ('bill') {
        <form #billEditor="ngForm" (ngSubmit)="billEditor.valid && saveBill()">
          <h2 class="first">Hóa đơn phòng {{ target?.name }} - tháng {{ api.month() | monthYear }}</h2>
          <label>Số điện mới (cũ: {{ target?.lastElectric }})<input name="electricNew" type="number" min="{{ target?.lastElectric }}" required [(ngModel)]="billForm.electricNew"></label>
          <div class="billing-note">Tiền điện: {{ electricUsage() }} kWh × {{ electricRate() | currency:'VND':'symbol':'1.0-0' }} = <b>{{ electricCharge() | currency:'VND':'symbol':'1.0-0' }}</b></div>
          @if (target?.waterByOccupant) {
            <div class="billing-note">Tiền nước: {{ target?.occupantCount }} người × {{ target?.waterRatePerPerson | currency:'VND':'symbol':'1.0-0' }} = <b>{{ waterCharge() | currency:'VND':'symbol':'1.0-0' }}</b></div>
          } @else {
            <label>Số nước mới (cũ: {{ target?.lastWater }})<input name="waterNew" type="number" min="{{ target?.lastWater }}" required [(ngModel)]="billForm.waterNew"></label>
          }
          <div class="billing-note">Tiền dịch vụ: {{ target?.serviceAmount | currency:'VND':'symbol':'1.0-0' }}</div>
          <label>Phí khác<input vndInput [allowNegative]="true" inputmode="decimal" name="other" required [(ngModel)]="billForm.other"></label>
          @if (billForm.other !== 0) {
            <label>Lý do phí khác<input name="otherNote" maxlength="200" required [(ngModel)]="billForm.otherNote"></label>
          }
          <div class="billing-total"><b>Tạm tính: {{ estimatedInvoiceTotal() | currency:'VND':'symbol':'1.0-0' }}</b></div>
          <div class="acts"><button type="button" (click)="closeEditor()">Hủy</button><button type="submit" class="p" [disabled]="busy() || billEditor.invalid">Lập hóa đơn</button></div>
        </form>
      }
      @case ('maintenance') {
        <form #maintenanceEditor="ngForm" (ngSubmit)="maintenanceEditor.valid && saveMaintenance()">
          <h2 class="first">Yêu cầu bảo trì phòng {{ target?.name }}</h2>
          <label>Thiết bị hư hỏng<textarea name="damaged" rows="3" maxlength="500" required [(ngModel)]="maintenanceForm.damagedEquipment"></textarea></label>
          <label>Ảnh hư hỏng (chọn nhiều ảnh, JPEG/PNG/WebP, tối đa 2 MB mỗi ảnh)<input type="file" multiple accept="image/jpeg,image/png,image/webp" (change)="selectMaintenanceFiles($event)"></label>
          @if (maintenanceForm.files.length) { <p class="mute">Đã chọn {{ maintenanceForm.files.length }} ảnh</p> }
          <div class="acts"><button type="button" (click)="closeEditor()">Hủy</button><button type="submit" class="p" [disabled]="busy() || maintenanceEditor.invalid">Gửi yêu cầu bảo trì</button></div>
        </form>
      }
    }
    </dialog>
    @if (error()) { <p class="error-banner" role="alert">{{ error() }} <button (click)="load()">Thử lại</button></p> }
    @if (loading()) { <div class="empty" role="status">Đang tải phòng…</div> }
    <div class="grid">
      @for (r of filtered(); track r.id) {
        <div class="card room">
          <div class="top"><span class="room-symbol"><app-icon name="room" /></span><span class="tag" [class.ok]="r.tenant">{{ r.tenant ? 'Đang thuê' : 'Trống' }}</span>@if (r.tenant; as t) { @if (t.deposit > 0) { <span class="tag ok">Đã cọc {{ t.deposit | currency: 'VND':'symbol':'1.0-0' }}</span> } }@if (r.reservedTenantName && !r.tenant) { <span class="tag warn">Đã giữ cọc</span> }@if (r.isUnderMaintenance) { <span class="tag warn">Bảo trì</span> }</div>
          <b class="room-name">{{ r.name }}@if (r.tenant; as t) { - {{ t.name }}{{ t.phone ? ' - ' + t.phone : '' }} }</b>
          <div class="mute">{{ r.propertyName }}</div>
          <div>{{ r.price | currency: 'VND':'symbol':'1.0-0' }}<span class="mute"> / tháng</span></div>
          <div class="mute">{{ r.occupantCount }} người · {{ r.waterByOccupant ? 'Nước theo đầu người' : 'Nước theo công tơ' }} · Dịch vụ {{ r.serviceAmount | currency:'VND':'symbol':'1.0-0' }}{{ r.electricRate != null ? ' · Điện riêng ' + (r.electricRate | currency:'VND':'symbol':'1.0-0') + '/kWh' : '' }}</div>
          @if (r.tenant; as t) {
            <div class="mute">Vào ở {{ t.startDate }}</div>
          } @else {
            <div class="mute">Điện {{ r.lastElectric }} · Nước {{ r.lastWater }}</div>
          }
          @if (r.reservedTenantName && !r.tenant) {
            <div class="mute">Giữ cọc: {{ r.reservedTenantName }}{{ r.reservedPhone ? ' - ' + r.reservedPhone : '' }} · {{ r.reservedDeposit | currency:'VND':'symbol':'1.0-0' }} · Cọc ngày {{ r.reservedDate }} · Giữ đến {{ r.reservedUntil }}</div>
          }
          @if (r.maintenanceNote) {
            <div class="mute">Hư hỏng: {{ r.maintenanceNote }}</div>
          }
          <div class="row room-actions">
            @if (r.tenant) {
              @if (!billed().has(r.id)) { <button class="s p" (click)="openBill(r)">Lập hóa đơn</button> }
              <button class="s" [disabled]="busy()" (click)="checkout(r)">Trả phòng</button>
            } @else if (!r.isUnderMaintenance) {
              <button class="s p" (click)="openRent(r)">Cho thuê</button>
              @if (r.reservedTenantName) {
                <button class="s" [disabled]="busy()" (click)="cancelReservation(r)">Hủy giữ cọc</button>
              } @else {
                <button class="s" (click)="openReserve(r)">Cọc giữ phòng</button>
              }
            }
            @if (!auth.isEmployee()) { <button class="icon-button" title="Sửa phòng" aria-label="Sửa phòng" (click)="editRoom(r)"><app-icon name="edit" /></button> }
            <button class="icon-button" [title]="'Ảnh phòng (' + r.imageCount + ')'" [attr.aria-label]="'Ảnh phòng ' + r.name" (click)="openImages(r)"><app-icon name="camera" /><span class="image-count">{{ r.imageCount }}</span></button>
            <button class="icon-button" [disabled]="busy()" [title]="maintenanceTitle(r)" [attr.aria-label]="maintenanceTitle(r)" (click)="maintenance(r)"><app-icon [name]="r.isUnderMaintenance || r.maintenanceNote ? 'check' : 'wrench'" /></button>
            @if (!auth.isEmployee()) { <button class="icon-button d" [disabled]="busy()" title="Xóa phòng" aria-label="Xóa phòng" (click)="remove(r)"><app-icon name="trash" /></button> }
          </div>
        </div>
      } @empty {
        @if (!loading()) { <div class="empty">Không có phòng phù hợp.</div> }
      }
    </div>
    <dialog #gallery class="modal gallery-modal" aria-label="Ảnh phòng" (cancel)="imageBusy() ? $event.preventDefault() : clearSelection()">
      <div class="modal-head"><div><h2>Ảnh phòng {{ imageRoom()?.name }}</h2><p class="mute">{{ imageRoom()?.propertyName }} · {{ images().length }}/8 ảnh</p></div><button class="icon-button" title="Đóng" aria-label="Đóng" (click)="closeImages()"><app-icon name="close" /></button></div>
      @if (imageError()) { <p class="err" role="alert">{{ imageError() }}</p> }
      @if (imagesLoading()) { <p role="status">Đang tải ảnh…</p> }
      <div class="gallery-grid">
        @for (image of images(); track image.id) {
          <figure><button class="photo-preview" [attr.aria-label]="image.caption || 'Xem ảnh phòng'" (click)="fullImage.set(image)"><img [src]="imageUrl(image)" [alt]="image.caption || 'Phòng ' + imageRoom()?.name"></button>
            <figcaption><span>{{ image.caption || imageRoom()?.name }}</span><button class="icon-button d" title="Xóa ảnh" aria-label="Xóa ảnh" [disabled]="imageBusy()" (click)="deleteImage(image)"><app-icon name="trash" /></button></figcaption></figure>
        } @empty { @if (!imagesLoading()) { <p class="empty">Chưa có ảnh phòng.</p> } }
      </div>
      @if (fullImage(); as image) {
        <div class="image-viewer"><button class="icon-button" title="Đóng ảnh lớn" aria-label="Đóng ảnh lớn" (click)="fullImage.set(null)"><app-icon name="close" /></button><img [src]="imageUrl(image)" [alt]="image.caption || 'Ảnh phòng'"></div>
      }
      <form class="upload-strip" (ngSubmit)="upload()">
        <label>Ảnh JPEG, PNG, WebP (tối đa 2 MB)<input #fileInput type="file" accept="image/jpeg,image/png,image/webp" [disabled]="imageBusy() || images().length >= 8" (change)="selectFile($event)"></label>
        @if (preview()) { <img class="upload-preview" [src]="preview()" alt="Ảnh chuẩn bị tải lên"> }
        <label>Chú thích<input name="caption" [(ngModel)]="caption" maxlength="200"></label>
        <div class="acts"><button type="submit" class="p" [disabled]="imageBusy() || !selectedFile || imagesLoading() || images().length >= 8"><app-icon name="upload" />{{ imageBusy() ? 'Đang xử lý…' : 'Tải ảnh lên' }}</button></div>
      </form>
    </dialog>
  `,
})
export class Rooms implements OnDestroy {
  api = inject(Api);
  auth = inject(Auth);
  private alerts = inject(Alerts);
  private editor = viewChild.required<ElementRef<HTMLDialogElement>>('editor');
  private gallery = viewChild.required<ElementRef<HTMLDialogElement>>('gallery');
  private fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  private revision = signal(0);
  rooms = signal<Room[]>([]);
  billed = signal(new Set<string>());
  panel = signal<Panel>(null);
  target: Room | null = null;
  roomForm = { propertyId: '', name: '', price: 3000000, lastElectric: 0, lastWater: 0,
    occupantCount: 1, waterByOccupant: false, serviceAmount: 0, electricRate: null as number | null };
  editRoomId = '';
  rentForm = { tenantName: '', phone: '', startDate: '', deposit: 0, expectedEndDate: '' };
  rentMonths: number | null = null;
  reserveForm = { tenantName: '', phone: '', deposit: 0, holdUntil: '' };
  cancelForm = { refund: 0 };
  billForm: { electricNew: number; waterNew: number | null; other: number; otherNote: string } = { electricNew: 0, waterNew: 0, other: 0, otherNote: '' };
  maintenanceForm: { damagedEquipment: string; files: File[] } = { damagedEquipment: '', files: [] };

  search = signal(''); status = signal(''); busy = signal(false); loading = signal(true); error = signal('');
  states = [{ value: '', label: 'Tất cả' }, { value: 'Vacant', label: 'Trống' }, { value: 'Reserved', label: 'Giữ cọc' }, { value: 'Occupied', label: 'Đang thuê' }, { value: 'Maintenance', label: 'Bảo trì' }];
  vacantCount = computed(() => this.rooms().filter(room => !room.tenant && !room.isUnderMaintenance && !room.reservedTenantName).length);
  filtered = computed(() => this.rooms().filter(room =>
    (!this.status() || this.status() === (room.tenant ? 'Occupied' : room.isUnderMaintenance ? 'Maintenance' : room.reservedTenantName ? 'Reserved' : 'Vacant')) &&
    `${room.name} ${room.propertyName} ${room.tenant?.name ?? ''}`.toLocaleLowerCase('vi').includes(this.search().trim().toLocaleLowerCase('vi'))));
  imageRoom = signal<Room | null>(null); images = signal<RoomImage[]>([]); imagesLoading = signal(false); imageBusy = signal(false); imageError = signal('');
  fullImage = signal<RoomImage | null>(null);
  selectedFile: File | null = null; caption = ''; preview = signal('');

  constructor() {
    effect(cleanup => {
      this.api.pid(); this.api.month(); this.revision(); this.loading.set(true); this.error.set(''); this.rooms.set([]);
      const invoices = this.auth.canAccessPage('invoices') ? this.api.invoices() : of([]);
      const sub = forkJoin({ rooms: this.api.rooms(), invoices }).subscribe({
        next: data => { this.rooms.set(data.rooms); this.billed.set(new Set(data.invoices.map(invoice => invoice.roomId))); this.loading.set(false); },
        error: err => { this.error.set(this.api.errorMessage(err)); this.loading.set(false); },
      });
      cleanup(() => sub.unsubscribe());
    });
  }

  load() {
    this.revision.update(value => value + 1);
  }

  private done = (message: string) => { this.busy.set(true); return { next: () => { this.busy.set(false); this.closeEditor(); this.load(); this.api.loadProperties(); this.alerts.success(message); }, error: (e: unknown) => { this.busy.set(false); this.api.say(this.api.errorMessage(e)); } }; };

  closeEditor() { if (this.busy()) return; this.editor().nativeElement.close(); this.panel.set(null); }

  openRoom() {
    const owned = this.api.properties().filter(property => property.canManage);
    if (!owned.length) return this.api.say('Chưa có nhà thuộc quyền quản lý');
    this.editRoomId = '';
    const propertyId = owned.find(property => property.id === this.api.pid())?.id || owned[0].id;
    const property = owned.find(item => item.id === propertyId)!;
    this.roomForm = { propertyId, name: '', price: 3000000, lastElectric: 0, lastWater: 0,
      occupantCount: 1, waterByOccupant: false, serviceAmount: property.serviceAmountPerMonth, electricRate: null };
    this.panel.set('room');
    this.editor().nativeElement.showModal();
  }
  editRoom(room: Room) {
    this.editRoomId = room.id;
    this.roomForm = { propertyId: room.propertyId, name: room.name, price: room.price, lastElectric: room.lastElectric,
      lastWater: room.lastWater, occupantCount: room.occupantCount, waterByOccupant: room.waterByOccupant, serviceAmount: room.serviceAmount, electricRate: room.electricRate };
    this.panel.set('room');
    this.editor().nativeElement.showModal();
  }

  selectProperty(propertyId: string) {
    this.roomForm.propertyId = propertyId;
    this.roomForm.serviceAmount = this.api.properties().find(property => property.id === propertyId)?.serviceAmountPerMonth ?? 0;
  }
  saveRoom() {
    if (!this.roomForm.name.trim()) return this.api.say('Vui lòng nhập tên phòng');
    const request = this.editRoomId ? this.api.updateRoom(this.editRoomId, this.roomForm) : this.api.addRoom(this.roomForm);
    request.subscribe(this.done(this.editRoomId ? 'Đã cập nhật phòng' : 'Đã thêm phòng'));
  }

  openRent(r: Room) {
    this.target = r;
    this.rentForm = { tenantName: r.reservedTenantName ?? '', phone: r.reservedPhone ?? '', startDate: new Date().toISOString().slice(0, 10), deposit: r.reservedDeposit ?? r.price, expectedEndDate: '' };
    this.rentMonths = null;
    this.panel.set('rent');
    this.editor().nativeElement.showModal();
  }

  today() { return new Date().toISOString().slice(0, 10); }

  // Contract ends the day before the same day-of-month after N months, clamped to month length.
  updateRentEndDate() {
    if (!this.rentMonths || this.rentMonths < 1 || !this.rentForm.startDate) return;
    const [year, month, day] = this.rentForm.startDate.split('-').map(Number);
    const firstOfTarget = new Date(year, month - 1 + this.rentMonths, 1);
    const daysInTarget = new Date(firstOfTarget.getFullYear(), firstOfTarget.getMonth() + 1, 0).getDate();
    const end = new Date(firstOfTarget.getFullYear(), firstOfTarget.getMonth(), Math.min(day, daysInTarget) - 1);
    this.rentForm.expectedEndDate = `${end.getFullYear()}-${String(end.getMonth() + 1).padStart(2, '0')}-${String(end.getDate()).padStart(2, '0')}`;
  }

  openReserve(r: Room) {
    this.target = r;
    this.reserveForm = { tenantName: '', phone: '', deposit: r.price, holdUntil: '' };
    this.panel.set('reserve');
    this.editor().nativeElement.showModal();
  }
  saveReserve() {
    if (!this.reserveForm.tenantName.trim()) return this.api.say('Vui lòng nhập tên khách đặt cọc');
    if (!this.reserveForm.holdUntil) return this.api.say('Vui lòng chọn ngày hết hạn giữ phòng');
    if (this.reserveForm.holdUntil < this.today()) return this.api.say('Hạn giữ phòng phải từ hôm nay trở đi');
    this.api.reserve(this.target!.id, { ...this.reserveForm, phone: this.reserveForm.phone || null }).subscribe(this.done('Đã cọc giữ phòng'));
  }
  cancelReservation(r: Room) {
    this.target = r;
    this.cancelForm = { refund: r.reservedDeposit ?? 0 };
    this.panel.set('cancel');
    this.editor().nativeElement.showModal();
  }
  saveCancelReservation() {
    const deposit = this.target?.reservedDeposit ?? 0;
    if (this.cancelForm.refund < 0 || this.cancelForm.refund > deposit) return this.api.say('Số tiền hoàn phải từ 0 đến tiền cọc');
    this.api.cancelReservation(this.target!.id, this.cancelForm.refund).subscribe(this.done('Đã hủy giữ cọc'));
  }
  saveRent() {
    if (!this.rentForm.tenantName.trim()) return this.api.say('Vui lòng nhập tên khách thuê');
    if (this.rentForm.expectedEndDate && this.rentForm.expectedEndDate < this.rentForm.startDate) return this.api.say('Ngày hết hạn phải từ ngày bắt đầu thuê');
    this.api.rent(this.target!.id, { ...this.rentForm, expectedEndDate: this.rentForm.expectedEndDate || null }).subscribe(this.done('Đã cho thuê phòng'));
  }

  openBill(r: Room) {
    this.target = r;
    this.billForm = { electricNew: r.lastElectric, waterNew: r.waterByOccupant ? null : r.lastWater, other: 0, otherNote: '' };
    this.panel.set('bill');
    this.editor().nativeElement.showModal();
  }
  saveBill() {
    if (this.billForm.other !== 0 && !this.billForm.otherNote.trim()) return this.api.say('Vui lòng nhập lý do phí khác');
    const waterNew = this.target!.waterByOccupant ? null : this.billForm.waterNew;
    this.api.createInvoice({ roomId: this.target!.id, month: this.api.month(), ...this.billForm, waterNew, otherNote: this.billForm.other !== 0 ? this.billForm.otherNote.trim() : null }).subscribe(this.done('Đã lập hóa đơn'));
  }

  electricRate() {
    return this.target?.electricRate ?? this.propertyElectricRate();
  }

  propertyElectricRate() {
    return this.api.properties().find(property => property.id === this.target?.propertyId)?.electricRate ?? 0;
  }

  electricUsage() {
    return Math.max(0, (this.billForm.electricNew ?? 0) - (this.target?.lastElectric ?? 0));
  }

  electricCharge() {
    return this.electricUsage() * this.electricRate();
  }

  waterCharge() {
    if (!this.target) return 0;
    if (this.target.waterByOccupant) return this.target.occupantCount * this.target.waterRatePerPerson;
    const waterRate = this.api.properties().find(property => property.id === this.target?.propertyId)?.waterRate ?? 0;
    return Math.max(0, (this.billForm.waterNew ?? this.target.lastWater) - this.target.lastWater) * waterRate;
  }

  estimatedInvoiceTotal() {
    if (!this.target) return 0;
    return this.target.price + this.electricCharge()
      + this.waterCharge() + this.target.serviceAmount + this.billForm.other;
  }

  async checkout(r: Room) {
    if (await this.alerts.confirm('Xác nhận trả phòng?', `Phòng ${r.name} sẽ được giải phóng.`, 'Trả phòng'))
      this.api.checkout(r.id).subscribe(this.done('Đã trả phòng'));
  }
  async remove(r: Room) {
    if (await this.alerts.confirm('Xóa phòng?', `Xóa phòng ${r.name} cùng toàn bộ hợp đồng, hóa đơn, ảnh và khiếu nại liên quan?`, 'Xóa phòng', true))
      this.api.deleteRoom(r.id).subscribe(this.done('Đã xóa phòng'));
  }

  maintenanceTitle(room: Room) {
    if (room.isUnderMaintenance) return 'Kết thúc bảo trì';
    return room.maintenanceNote ? 'Hủy yêu cầu bảo trì' : 'Yêu cầu bảo trì';
  }

  async maintenance(room: Room) {
    if (room.isUnderMaintenance) {
      if (await this.alerts.confirm('Kết thúc bảo trì?', `Phòng ${room.name}`, 'Kết thúc bảo trì'))
        this.api.maintenance(room.id, false).subscribe(this.done('Đã cập nhật trạng thái bảo trì'));
      return;
    }
    if (room.maintenanceNote) {
      if (await this.alerts.confirm('Hủy yêu cầu bảo trì?', `Phòng ${room.name}`, 'Hủy yêu cầu'))
        this.api.maintenance(room.id, false).subscribe(this.done('Đã hủy yêu cầu bảo trì'));
      return;
    }
    this.target = room;
    this.maintenanceForm = { damagedEquipment: '', files: [] };
    this.panel.set('maintenance');
    this.editor().nativeElement.showModal();
  }
  selectMaintenanceFiles(event: Event) {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    const limit = 8 - (this.target?.imageCount ?? 0);
    if (files.length > limit) return this.api.say(`Phòng chỉ còn ${limit} chỗ cho ảnh`);
    if (files.some(file => !['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 2_097_152))
      return this.api.say('Chỉ chọn ảnh JPEG, PNG hoặc WebP không quá 2 MB');
    this.maintenanceForm.files = files;
  }
  saveMaintenance() {
    const room = this.target!;
    const { damagedEquipment, files } = this.maintenanceForm;
    this.busy.set(true);
    this.api.maintenance(room.id, true, damagedEquipment.trim()).pipe(
      switchMap(() => concat(...files.map(file => this.api.uploadImage(room.id, file, 'Hư hỏng'))).pipe(toArray())),
    ).subscribe({
      next: () => { this.busy.set(false); this.closeEditor(); this.load(); this.api.loadProperties(); this.alerts.success('Đã gửi yêu cầu bảo trì'); },
      error: e => { this.busy.set(false); this.load(); this.api.say(this.api.errorMessage(e)); },
    });
  }
  openImages(room: Room) {
    this.imageRoom.set(room); this.images.set([]); this.fullImage.set(null); this.imageError.set(''); this.clearSelection();
    this.gallery().nativeElement.showModal(); this.loadImages();
  }
  closeImages() { if (this.imageBusy()) return; this.gallery().nativeElement.close(); this.clearSelection(); }
  clearSelection(clearInput = true) { if (this.preview()) URL.revokeObjectURL(this.preview()); this.preview.set(''); this.selectedFile = null; this.caption = ''; if (clearInput) this.fileInput().nativeElement.value = ''; }
  loadImages() {
    const room = this.imageRoom(); if (!room) return;
    this.imagesLoading.set(true);
    this.api.images(room.id).pipe(finalize(() => this.imagesLoading.set(false))).subscribe({ next: images => this.images.set(images), error: err => this.imageError.set(this.api.errorMessage(err)) });
  }
  selectFile(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    this.clearSelection(false); this.imageError.set('');
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 2_097_152) { this.imageError.set('Chọn ảnh JPEG, PNG hoặc WebP không quá 2 MB'); return; }
    this.selectedFile = file; this.preview.set(URL.createObjectURL(file));
  }
  imageUrl(image: RoomImage) { return 'data:' + image.contentType + ';base64,' + image.base64; }
  upload() {
    const room = this.imageRoom(); if (!room || !this.selectedFile || this.imageBusy()) return;
    this.imageBusy.set(true); this.imageError.set('');
    this.api.uploadImage(room.id, this.selectedFile, this.caption).pipe(finalize(() => this.imageBusy.set(false))).subscribe({
      next: () => { this.clearSelection(); this.loadImages(); this.load(); this.alerts.success('Đã tải ảnh lên'); }, error: err => this.imageError.set(this.api.errorMessage(err)),
    });
  }
  async deleteImage(image: RoomImage) {
    const room = this.imageRoom();
    if (!room || !await this.alerts.confirm('Xóa ảnh phòng?', 'Ảnh này sẽ bị xóa vĩnh viễn.', 'Xóa ảnh', true, this.gallery().nativeElement)) return;
    this.imageBusy.set(true); this.imageError.set('');
    this.api.deleteImage(room.id, image.id).pipe(finalize(() => this.imageBusy.set(false))).subscribe({ next: () => { this.loadImages(); this.load(); this.alerts.success('Đã xóa ảnh'); }, error: err => this.imageError.set(this.api.errorMessage(err)) });
  }
  ngOnDestroy() { if (this.preview()) URL.revokeObjectURL(this.preview()); }
}
