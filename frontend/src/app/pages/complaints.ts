import { DatePipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { Api } from '../core/api';
import { Icon } from '../core/icon';
import { Assignee, Complaint, ComplaintStatus, Room, complaintLabels } from '../core/models';
import { Alerts } from '../core/alerts';

@Component({
  selector: 'app-complaints', imports: [FormsModule, DatePipe, Icon],
  template: `
    <div class="page-head"><div><h2>Khiếu nại</h2><p class="mute">{{ openCount() }} chưa hoàn tất · {{ complaints().length }} tổng cộng</p></div><button class="p" (click)="create()"><app-icon name="plus" />Tiếp nhận khiếu nại</button></div>
    <div class="toolbar"><input type="search" aria-label="Tìm khiếu nại" placeholder="Nội dung, khách thuê, phòng…" [ngModel]="search()" (ngModelChange)="search.set($event)">
      <select aria-label="Lọc trạng thái khiếu nại" [ngModel]="status()" (ngModelChange)="status.set($event)"><option value="">Tất cả trạng thái</option>@for (state of states; track state) { <option [value]="state">{{ labels[state] }}</option> }</select>
    </div>
    @if (error()) { <div class="error-banner" role="alert">{{ error() }} <button (click)="reload()">Thử lại</button></div> }
    @if (loading()) { <p class="empty" role="status">Đang tải khiếu nại…</p> }
    @else {
      <div class="tbl"><table><thead><tr><th>Khiếu nại</th><th>Nhà / Phòng</th><th>Ưu tiên</th><th>Trạng thái</th><th>Người xử lý</th><th><span class="sr-only">Thao tác</span></th></tr></thead><tbody>
        @for (complaint of filtered(); track complaint.id) {
          <tr><td><b>{{ complaint.title }}</b><div class="mute">{{ complaint.tenantName }} · {{ complaint.createdAt | date:'dd/MM/yyyy' }}</div></td>
            <td>{{ complaint.roomName }}<div class="mute">{{ complaint.propertyName }}</div></td>
            <td><span class="tag" [class.bad]="complaint.priority === 'Urgent'">{{ priorities[complaint.priority] }}</span></td>
            <td><span class="tag" [class.ok]="complaint.status === 'Resolved' || complaint.status === 'Closed'" [class.warn]="complaint.status === 'InProgress'">{{ labels[complaint.status] }}</span></td>
            <td>{{ complaint.assignedName || 'Chưa phân công' }}</td>
            <td class="n nowrap"><button class="icon-button" title="Chi tiết và xử lý" aria-label="Chi tiết và xử lý" (click)="edit(complaint)"><app-icon name="edit" /></button>
              <button class="s d" title="Xóa khiếu nại" aria-label="Xóa khiếu nại" (click)="remove(complaint)"><app-icon name="trash" /></button></td></tr>
        } @empty { <tr><td colspan="6" class="empty">Không có khiếu nại phù hợp.</td></tr> }
      </tbody></table></div>
    }
    <dialog #editor class="modal" aria-label="Thông tin khiếu nại" (cancel)="busy() && $event.preventDefault()">
      <form #complaintForm="ngForm" (ngSubmit)="complaintForm.valid && save()">
        <div class="modal-head"><h2>{{ editing() ? 'Xử lý khiếu nại' : 'Tiếp nhận khiếu nại' }}</h2><button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="close()"><app-icon name="close" /></button></div>
        @if (editing(); as complaint) {
          <div class="complaint-detail"><b>{{ complaint.title }}</b><p class="mute">{{ complaint.propertyName }} / {{ complaint.roomName }} · {{ complaint.tenantName }}</p><p class="pre-wrap">{{ complaint.description }}</p></div>
          <div class="form-grid">
            <label>Trạng thái<select name="status" [(ngModel)]="update.status">@for (state of states; track state) { <option [value]="state">{{ labels[state] }}</option> }</select></label>
            <label>Người xử lý<select name="assigned" [(ngModel)]="update.assignedTo"><option value="">Chưa phân công</option>@for (person of assignees(); track person.id) { <option [value]="person.id">{{ person.fullName }}</option> }</select></label>
            <label class="span-2">Kết quả xử lý<textarea name="resolution" [(ngModel)]="update.resolution" rows="4" maxlength="2000" [required]="update.status === 'Resolved' || update.status === 'Closed'"></textarea></label>
          </div>
        } @else {
          <div class="form-grid">
            <label>Phòng<select name="room" [(ngModel)]="form.roomId" (ngModelChange)="selectRoom()" required><option value="">Chọn phòng</option>@for (room of rooms(); track room.id) { <option [value]="room.id">{{ room.propertyName }} / {{ room.name }}</option> }</select></label>
            <label>Khách phản ánh<input name="tenant" [(ngModel)]="form.tenantName" required maxlength="200"></label>
            <label class="span-2">Tiêu đề<input name="title" [(ngModel)]="form.title" required maxlength="200"></label>
            <label>Mức ưu tiên<select name="priority" [(ngModel)]="form.priority"><option value="Low">Thấp</option><option value="Normal">Bình thường</option><option value="Urgent">Khẩn cấp</option></select></label>
            <label class="span-2">Nội dung<textarea name="description" [(ngModel)]="form.description" rows="4" required maxlength="4000"></textarea></label>
          </div>
        }
        @if (saveError()) { <p class="err" role="alert">{{ saveError() }}</p> }
        <div class="acts"><button type="button" (click)="close()">Hủy</button><button type="submit" class="p" [disabled]="busy() || complaintForm.invalid || assigneesLoading() || (editing() && (update.status === 'Resolved' || update.status === 'Closed') && !update.resolution.trim())"><app-icon name="save" />{{ busy() ? 'Đang lưu…' : 'Lưu khiếu nại' }}</button></div>
      </form>
    </dialog>
  `,
})
export class Complaints {
  api = inject(Api);
  private alerts = inject(Alerts);
  private editor = viewChild.required<ElementRef<HTMLDialogElement>>('editor');
  private revision = signal(0);
  complaints = signal<Complaint[]>([]);
  rooms = signal<Room[]>([]);
  assignees = signal<Assignee[]>([]);
  assigneesLoading = signal(false);
  search = signal(''); status = signal(''); loading = signal(true); busy = signal(false);
  error = signal(''); saveError = signal(''); editing = signal<Complaint | null>(null);
  labels = complaintLabels;
  states = Object.keys(complaintLabels) as ComplaintStatus[];
  priorities = { Low: 'Thấp', Normal: 'Bình thường', Urgent: 'Khẩn cấp' };
  form = { roomId: '', tenantName: '', title: '', description: '', priority: 'Normal' };
  update: { status: ComplaintStatus; assignedTo: string; resolution: string } = { status: 'Open', assignedTo: '', resolution: '' };
  openCount = computed(() => this.complaints().filter(complaint => complaint.status === 'Open' || complaint.status === 'InProgress').length);
  filtered = computed(() => {
    const text = this.search().trim().toLocaleLowerCase('vi');
    return this.complaints().filter(complaint => (!this.status() || complaint.status === this.status()) &&
      `${complaint.title} ${complaint.tenantName} ${complaint.roomName} ${complaint.propertyName}`.toLocaleLowerCase('vi').includes(text));
  });
  constructor() {
    effect(cleanup => {
      this.api.pid(); this.revision(); this.loading.set(true); this.error.set(''); this.complaints.set([]); this.rooms.set([]);
      const sub = forkJoin({ complaints: this.api.complaints(), rooms: this.api.rooms() }).subscribe({
        next: data => { this.complaints.set(data.complaints); this.rooms.set(data.rooms); this.loading.set(false); },
        error: err => { this.error.set(this.api.errorMessage(err)); this.loading.set(false); },
      });
      cleanup(() => sub.unsubscribe());
    });
  }
  reload() { this.revision.update(value => value + 1); }
  create() {
    if (!this.rooms().length) return this.api.say('Chưa có phòng để tiếp nhận khiếu nại');
    this.editing.set(null); this.saveError.set(''); this.assigneesLoading.set(false);
    this.form = { roomId: '', tenantName: '', title: '', description: '', priority: 'Normal' };
    this.editor().nativeElement.showModal();
  }
  selectRoom() { this.form.tenantName = this.rooms().find(room => room.id === this.form.roomId)?.tenant?.name ?? ''; }
  edit(complaint: Complaint) {
    this.editing.set(complaint); this.saveError.set(''); this.assignees.set([]); this.assigneesLoading.set(true);
    this.update = { status: complaint.status, assignedTo: complaint.assignedTo ?? '', resolution: complaint.resolution };
    this.api.assignees(complaint.propertyId).subscribe({
      next: people => { this.assignees.set(people); if (!people.some(person => person.id === this.update.assignedTo)) this.update.assignedTo = ''; this.assigneesLoading.set(false); },
      error: err => { this.saveError.set(this.api.errorMessage(err)); this.assigneesLoading.set(false); },
    });
    this.editor().nativeElement.showModal();
  }
  close() { if (this.busy()) return; this.editor().nativeElement.close(); this.editing.set(null); }
  async remove(complaint: Complaint) {
    if (!await this.alerts.confirm('Xóa khiếu nại?', complaint.title, 'Xóa', true)) return;
    this.api.deleteComplaint(complaint.id).subscribe({
      next: () => { this.reload(); this.alerts.success('Đã xóa khiếu nại'); },
      error: err => this.api.say(this.api.errorMessage(err)),
    });
  }
  save() {
    if (this.busy()) return;
    const current = this.editing();
    if (current && (this.update.status === 'Resolved' || this.update.status === 'Closed') && !this.update.resolution.trim()) { this.saveError.set('Vui lòng nhập kết quả xử lý'); return; }
    const request = current ? this.api.updateComplaint(current.id, { ...this.update, assignedTo: this.update.assignedTo || null }) : this.api.addComplaint(this.form);
    this.busy.set(true); this.saveError.set('');
    request.pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => { this.editor().nativeElement.close(); this.editing.set(null); this.reload(); this.alerts.success(current ? 'Đã cập nhật khiếu nại' : 'Đã tiếp nhận khiếu nại'); }, error: err => this.saveError.set(this.api.errorMessage(err)) });
  }
}
