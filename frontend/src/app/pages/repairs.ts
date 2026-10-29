import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { Api } from '../core/api';
import { Icon } from '../core/icon';
import { RepairRequest, RepairStatus, repairLabels } from '../core/models';
import { Alerts } from '../core/alerts';

@Component({
  selector: 'app-repairs', imports: [FormsModule, Icon],
  template: `
    <div class="page-head"><div><h2>Yêu cầu sửa chữa</h2><p class="mute">{{ openCount() }} chưa hoàn tất · {{ repairs().length }} tổng cộng</p></div></div>
    <div class="toolbar"><input type="search" aria-label="Tìm yêu cầu sửa chữa" placeholder="Phòng, khách thuê, thiết bị…" [ngModel]="search()" (ngModelChange)="search.set($event)">
      <select aria-label="Lọc trạng thái sửa chữa" [ngModel]="status()" (ngModelChange)="status.set($event)"><option value="">Tất cả trạng thái</option>@for (state of states; track state) { <option [value]="state">{{ labels[state] }}</option> }</select>
    </div>
    @if (error()) { <div class="error-banner" role="alert">{{ error() }} <button (click)="reload()">Thử lại</button></div> }
    @if (loading()) { <p class="empty" role="status">Đang tải yêu cầu sửa chữa…</p> }
    @else {
      <div class="tbl"><table><thead><tr><th>Phòng</th><th>Thiết bị hư hỏng</th><th>Khách thuê</th><th>Trạng thái</th></tr></thead><tbody>
        @for (repair of filtered(); track repair.roomId) {
          <tr><td><b>{{ repair.roomName }}</b><div class="mute">{{ repair.propertyName }}</div></td>
            <td class="pre-wrap">{{ repair.description }}</td>
            <td>{{ repair.tenantName || 'Phòng trống' }}</td>
            <td><select [attr.aria-label]="'Trạng thái sửa chữa phòng ' + repair.roomName" [ngModel]="repair.status" [disabled]="busyRoomId() === repair.roomId" (ngModelChange)="changeStatus(repair, $event)">@for (state of states; track state) { <option [value]="state">{{ labels[state] }}</option> }</select></td></tr>
        } @empty { <tr><td colspan="4" class="empty">Không có yêu cầu sửa chữa phù hợp.</td></tr> }
      </tbody></table></div>
    }
  `,
})
export class Repairs {
  api = inject(Api);
  private alerts = inject(Alerts);
  private revision = signal(0);
  repairs = signal<RepairRequest[]>([]);
  search = signal(''); status = signal(''); loading = signal(true); error = signal(''); busyRoomId = signal('');
  labels = repairLabels;
  states = Object.keys(repairLabels) as RepairStatus[];
  openCount = computed(() => this.repairs().filter(repair => repair.status === 'New' || repair.status === 'InProgress').length);
  filtered = computed(() => {
    const text = this.search().trim().toLocaleLowerCase('vi');
    return this.repairs().filter(repair => (!this.status() || repair.status === this.status()) &&
      `${repair.roomName} ${repair.propertyName} ${repair.tenantName ?? ''} ${repair.description}`.toLocaleLowerCase('vi').includes(text));
  });
  constructor() {
    effect(cleanup => {
      this.api.pid(); this.revision(); this.loading.set(true); this.error.set('');
      const sub = this.api.repairs().subscribe({
        next: repairs => { this.repairs.set(repairs); this.loading.set(false); },
        error: err => { this.error.set(this.api.errorMessage(err)); this.loading.set(false); },
      });
      cleanup(() => sub.unsubscribe());
    });
  }
  reload() { this.revision.update(value => value + 1); }
  changeStatus(repair: RepairRequest, status: RepairStatus) {
    if (status === repair.status || this.busyRoomId()) return;
    this.busyRoomId.set(repair.roomId);
    this.api.updateRepair(repair.roomId, status).pipe(finalize(() => this.busyRoomId.set(''))).subscribe({
      next: () => { this.alerts.success('Đã cập nhật trạng thái sửa chữa'); this.reload(); },
      error: err => { this.api.say(this.api.errorMessage(err)); this.reload(); },
    });
  }
}
