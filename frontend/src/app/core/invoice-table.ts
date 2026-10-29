import { CurrencyPipe } from '@angular/common';
import { Component, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import html2canvas from 'html2canvas';
import { Api } from './api';
import { Invoice } from './models';
import { Alerts } from './alerts';
import { formatMonthYear } from './month-year';
import { Icon } from './icon';
import { bankBin, buildVietQrPayload, renderQr, transferContent } from './vietqr';

@Component({
  selector: 'app-invoice-table',
  imports: [CurrencyPipe, Icon],
  template: `
    @if (list().length) {
      <div class="tbl">
        <table>
          <thead><tr><th>Phòng</th><th>Khách thuê</th><th class="n">Điện</th><th class="n">Nước</th><th class="n">Dịch vụ</th><th class="n">Tổng</th><th>Trạng thái</th><th></th></tr></thead>
          <tbody>
            @for (i of list(); track i.id) {
              <tr>
                <td><b>{{ i.roomName }}</b><div class="mute">{{ i.propertyName }}</div></td>
                <td>{{ i.tenantName }}</td>
                <td class="n">{{ i.electricAmount | currency: 'VND':'symbol':'1.0-0' }}<div class="mute">{{ i.electricNew - i.electricOld }} kWh × {{ i.electricRate | currency:'VND':'symbol':'1.0-0' }}</div></td>
                <td class="n">{{ i.waterAmount | currency: 'VND':'symbol':'1.0-0' }}<div class="mute">{{ i.waterByOccupant ? i.occupantCount + ' người × ' + (i.waterRatePerPerson | currency:'VND':'symbol':'1.0-0') : (i.waterNew - i.waterOld) + ' m³' }}</div></td>
                <td class="n">{{ i.serviceAmount | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td class="n"><b>{{ i.total | currency: 'VND':'symbol':'1.0-0' }}</b></td>
                <td><span class="tag" [class.ok]="i.isPaid" [class.warn]="!i.isPaid">{{ i.isPaid ? 'Đã thu' : 'Chưa thu' }}</span></td>
                <td class="n">
                  <button class="s" (click)="show(i)">Xem chi tiết</button>
                  <button class="s" (click)="copy(i)">Sao chép</button>
                  @if (!i.isPaid) { <button class="s d" title="Xóa hóa đơn" aria-label="Xóa hóa đơn" (click)="remove(i)"><app-icon name="trash" /></button> }
                  <button class="s p" (click)="pay(i)">{{ i.isPaid ? 'Hủy thu' : 'Đã thu' }}</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <div class="card empty">{{ empty() }}</div>
    }
    <dialog #detailDialog class="modal" aria-label="Chi tiết hóa đơn" (close)="onDetailClosed()">
      @if (detail(); as i) {
        <div class="modal-head"><div><h2>Hóa đơn phòng {{ i.roomName }}</h2><p class="mute">{{ i.propertyName }} · tháng {{ monthText(i) }}</p></div><button class="icon-button" title="Đóng" aria-label="Đóng" (click)="closeDetail()"><app-icon name="close" /></button></div>
        <table>
          <tbody>
            <tr><td>Khách thuê</td><td class="n">{{ honorific(i) }}{{ i.tenantName }}</td></tr>
            <tr><td>Tiền phòng</td><td class="n">{{ i.rent | currency: 'VND':'symbol':'1.0-0' }}</td></tr>
            <tr><td>Điện ({{ i.electricOld }} → {{ i.electricNew }} = {{ i.electricNew - i.electricOld }} kWh × {{ i.electricRate | currency:'VND':'symbol':'1.0-0' }})</td><td class="n">{{ i.electricAmount | currency: 'VND':'symbol':'1.0-0' }}</td></tr>
            <tr><td>Nước ({{ i.waterByOccupant ? i.occupantCount + ' người × ' + (i.waterRatePerPerson | currency:'VND':'symbol':'1.0-0') : i.waterOld + ' → ' + i.waterNew }})</td><td class="n">{{ i.waterAmount | currency: 'VND':'symbol':'1.0-0' }}</td></tr>
            @if (i.serviceAmount) { <tr><td>Dịch vụ</td><td class="n">{{ i.serviceAmount | currency: 'VND':'symbol':'1.0-0' }}</td></tr> }
            @if (i.other) { <tr><td>Phí khác{{ i.otherNote ? ' (' + i.otherNote + ')' : '' }}</td><td class="n">{{ i.other | currency: 'VND':'symbol':'1.0-0' }}</td></tr> }
          </tbody>
          <tfoot><tr><th>Tổng cộng</th><th class="n"><b>{{ i.total | currency: 'VND':'symbol':'1.0-0' }}</b></th></tr></tfoot>
        </table>
        @if (qrUrl(); as url) {
          <div class="qr-transfer">
            <p class="mute">Chuyển khoản bằng mã QR · Nội dung: {{ transferText(i) }}</p>
            <img [src]="url" alt="Mã QR chuyển khoản" style="max-width:200px">
          </div>
        } @else if (qrError()) {
          <p class="mute">{{ qrError() }}</p>
        }
        <div class="modal-actions no-capture"><button class="s p" (click)="sendToTenant()">Gửi khách</button></div>
      }
    </dialog>
  `,
})
export class InvoiceTable {
  private api = inject(Api);
  private alerts = inject(Alerts);
  list = input.required<Invoice[]>();
  empty = input('Chưa có hóa đơn nào.');
  changed = output<void>();
  detail = signal<Invoice | null>(null);
  qrUrl = signal<string | null>(null);
  qrError = signal('');
  private detailDialog = viewChild.required<ElementRef<HTMLDialogElement>>('detailDialog');

  monthText(i: Invoice) { return formatMonthYear(i.month); }

  honorific(i: Invoice) { return i.gender === 'Nam' ? 'Anh ' : i.gender === 'Nữ' ? 'Chị ' : ''; }

  transferText(i: Invoice) { return transferContent(`${i.tenantName}-${i.roomName}-${i.propertyName}-${i.month.slice(5, 7)}`); }

  show(i: Invoice) {
    this.detail.set(i);
    this.qrError.set('');
    this.detailDialog().nativeElement.showModal();
    const property = this.api.properties().find(p => p.id === i.propertyId);
    if (!property?.bankAccountNumber) {
      this.qrError.set('Chưa có số tài khoản nhận tiền. Vui lòng cập nhật trong thông tin nhà trọ.');
      return;
    }
    const bin = bankBin(property.bankName);
    if (!bin) {
      this.qrError.set(`Không nhận diện được ngân hàng "${property.bankName}". Nhập đúng tên ngân hàng (ví dụ Vietcombank) hoặc mã BIN 6 số.`);
      return;
    }
    const payload = buildVietQrPayload({ bin, accountNumber: property.bankAccountNumber, amount: i.total, content: this.transferText(i) });
    renderQr(payload).then(
      url => { if (this.detail() === i) this.qrUrl.set(url); },
      () => { if (this.detail() === i) this.qrError.set('Không tạo được mã QR.'); },
    );
  }

  closeDetail() { this.detailDialog().nativeElement.close(); }

  async sendToTenant() {
    try {
      const canvas = await html2canvas(this.detailDialog().nativeElement, {
        scale: 2,
        backgroundColor: '#ffffff',
        ignoreElements: el => el.classList.contains('no-capture'),
      });
      const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/png'));
      if (!blob) throw new Error('Không tạo được ảnh hóa đơn.');
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
      window.open('https://chat.zalo.me/', '_blank', 'noopener');
      this.alerts.success('Đã chụp hóa đơn. Mở Zalo, chọn khách và dán (Ctrl+V).');
    } catch (e) {
      this.api.say(e instanceof Error ? e.message : 'Không gửi được hóa đơn.');
    }
  }

  onDetailClosed() {
    this.detail.set(null);
    this.qrUrl.set(null);
    this.qrError.set('');
  }

  remove(i: Invoice) {
    this.alerts.confirm('Xóa hóa đơn?', `Hóa đơn phòng ${i.roomName} tháng ${formatMonthYear(i.month)} sẽ bị xóa.`, 'Xóa', true).then(ok => {
      if (!ok) return;
      this.api.deleteInvoice(i.id).subscribe({
        next: () => { this.changed.emit(); this.alerts.success('Đã xóa hóa đơn'); },
        error: e => this.api.say(this.api.errorMessage(e)),
      });
    });
  }

  pay(i: Invoice) {
    this.api.togglePaid(i.id).subscribe({
      next: () => { this.changed.emit(); this.alerts.success(i.isPaid ? 'Đã hủy thu tiền' : 'Đã ghi nhận thu tiền'); },
      error: e => this.api.say(this.api.errorMessage(e)),
    });
  }

  copy(i: Invoice) {
    const vnd = (n: number) => n.toLocaleString('vi-VN') + ' ₫';
    const text = [
      `Hóa đơn phòng ${i.roomName} - ${i.propertyName} - tháng ${formatMonthYear(i.month)}`,
      `Tiền phòng: ${vnd(i.rent)}`,
      `Điện (${i.electricOld} → ${i.electricNew} = ${i.electricNew - i.electricOld} kWh x ${vnd(i.electricRate)}): ${vnd(i.electricAmount)}`,
      `Nước: ${i.waterByOccupant ? `${i.occupantCount} người x ${vnd(i.waterRatePerPerson)}` : `${i.waterOld} → ${i.waterNew}`}: ${vnd(i.waterAmount)}`,
      ...(i.serviceAmount ? [`Dịch vụ: ${vnd(i.serviceAmount)}`] : []),
      ...(i.other ? [`Phí khác${i.otherNote ? ` (${i.otherNote})` : ''}: ${vnd(i.other)}`] : []),
      `Tổng cộng: ${vnd(i.total)}`,
    ].join('\n');
    navigator.clipboard.writeText(text).then(
      () => this.api.say('Đã sao chép nội dung gửi khách'),
      () => this.api.say('Không thể sao chép'),
    );
  }
}
