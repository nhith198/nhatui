import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { DepositReceipt, FinancialExpense, FinancialReport } from '../core/models';
import { Icon } from '../core/icon';
import { VndInput } from '../core/vnd-input';
import { Alerts } from '../core/alerts';
import { MonthYearPipe } from '../core/month-year';

type ExpenseCategory = FinancialExpense['category'];

@Component({
  selector: 'app-financials',
  imports: [CurrencyPipe, DatePipe, FormsModule, Icon, VndInput, MonthYearPipe],
  template: `
    <div class="page-head">
      <div><h2>Tài chính</h2><p class="mute">Thu thực nhận, chi phí và lợi nhuận theo tháng</p></div>
      <button class="p" (click)="add()"><app-icon name="plus" />Ghi khoản chi</button>
    </div>

    <dialog #editor class="modal compact-modal" aria-label="Khoản chi" (close)="editing.set(false)">
      <form #expenseForm="ngForm" (ngSubmit)="expenseForm.valid && save()">
        <div class="modal-head"><h2>{{ editId ? 'Sửa khoản chi' : 'Ghi khoản chi' }}</h2><button type="button" class="icon-button" title="Đóng" aria-label="Đóng" (click)="cancel()"><app-icon name="close" /></button></div>
        <div class="financial-form-grid">
          <label>Ngày chi<input name="date" type="date" [(ngModel)]="form.date" required></label>
          <label>Loại chi
            <select name="category" [(ngModel)]="form.category">
              <option value="Operating">Vận hành</option>
              <option value="Sales">Sale phòng</option>
              <option value="Other">Khác</option>
            </select>
          </label>
          <label>Số tiền<input vndInput inputmode="decimal" name="amount" min="1" max="1000000000" [(ngModel)]="form.amount" required></label>
          <label>Nhà
            <select name="propertyId" [(ngModel)]="form.propertyId">
              @if (!auth.isEmployee()) { <option value="">Chi phí chung</option> }
              @for (property of api.properties(); track property.id) { <option [value]="property.id">{{ property.name }}</option> }
            </select>
          </label>
          <label class="span-2">Nội dung<input name="description" maxlength="200" [(ngModel)]="form.description" placeholder="Ví dụ: Hoa hồng phòng 101" required></label>
          <label class="check-row span-2"><input type="checkbox" name="isPaid" [(ngModel)]="form.isPaid"><span>Đã chi</span></label>
          <div class="span-2 recurring-field">
            <label class="check-row"><input type="checkbox" name="isMonthly" [(ngModel)]="form.isMonthly"><span>Khoản chi cố định hàng tháng</span></label>
            <small class="mute">Tự động sinh khoản chi cùng nội dung vào ngày tương ứng mỗi tháng tiếp theo</small>
          </div>
          <label class="span-2">Người nhận / sale<input name="recipient" required maxlength="200" [(ngModel)]="form.recipient" placeholder="Tên người nhận khoản chi"></label>
        </div>
        <div class="acts"><button type="button" (click)="cancel()">Hủy</button><button class="p" type="submit" [disabled]="expenseForm.invalid">Lưu khoản chi</button></div>
      </form>
    </dialog>

    @if (report(); as data) {
      <div class="stats">
        <div class="stat"><b>{{ data.collected | currency: 'VND':'symbol':'1.0-0' }}</b><span>Đã thu tháng {{ api.month() | monthYear }}</span></div>
        <div class="stat"><b>{{ data.expenses | currency: 'VND':'symbol':'1.0-0' }}</b><span>Tổng chi</span></div>
        <div class="stat"><b>{{ data.profit | currency: 'VND':'symbol':'1.0-0' }}</b><span>Lợi nhuận</span></div>
        <div class="stat"><b>{{ salesTotal() | currency: 'VND':'symbol':'1.0-0' }}</b><span>Chi phí sale phòng</span></div>
      </div>

      <h2><button type="button" class="section-toggle" [attr.aria-expanded]="!collapsed().expenses" (click)="toggle('expenses')"><span>{{ collapsed().expenses ? '▸' : '▾' }}</span>Khoản chi tháng {{ api.month() | monthYear }} ({{ data.expenseEntries.length }})</button></h2>
      @if (!collapsed().expenses) {
      @if (data.expenseEntries.length) {
        <div class="tbl"><table>
          <thead><tr><th>Ngày</th><th>Nội dung</th><th>Người nhận</th><th>Nhà</th><th class="n">Số tiền</th><th>Đã chi</th><th></th></tr></thead>
          <tbody>
            @for (entry of data.expenseEntries; track entry.id) {
              <tr>
                <td class="nowrap">{{ entry.date | date: 'dd/MM/yyyy' }}</td>
                <td><b>{{ entry.description }}</b><div class="mute">{{ categoryLabel(entry.category) }} · {{ entry.isPaid ? 'Đã chi' : 'Dự chi' }}@if (entry.isGenerated) { · Tự động }</div></td>
                <td>{{ entry.recipient || '—' }}</td><td>{{ entry.propertyName || 'Chi phí chung' }}</td>
                <td class="n">{{ entry.amount | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td><input type="checkbox" aria-label="Đã chi" [checked]="entry.isPaid" [disabled]="entry.isPaid" (change)="togglePaid(entry, $any($event.target).checked)"></td>
                <td class="n nowrap">@if (!entry.isPaid) {<button class="s" title="Sửa khoản chi" aria-label="Sửa khoản chi" (click)="edit(entry)"><app-icon name="edit" /></button>
                  <button class="s d" title="Xóa khoản chi" aria-label="Xóa khoản chi" (click)="remove(entry)"><app-icon name="trash" /></button>}</td>
              </tr>
            }
          </tbody>
        </table></div>
      } @else {
        <div class="empty">Chưa có khoản chi nào trong tháng này.</div>
      }
      }

      <h2><button type="button" class="section-toggle" [attr.aria-expanded]="!collapsed().deposits" (click)="toggle('deposits')"><span>{{ collapsed().deposits ? '▸' : '▾' }}</span>Phiếu xác nhận cọc tháng {{ api.month() | monthYear }} ({{ data.depositReceipts.length }})</button></h2>
      @if (!collapsed().deposits) {
      @if (data.depositReceipts.length) {
        <div class="tbl"><table>
          <thead><tr><th>Ngày nhận</th><th>Phòng</th><th>Khách</th><th>Giữ đến</th><th>Trạng thái</th><th class="n">Số tiền</th><th><span class="sr-only">Thao tác</span></th></tr></thead>
          <tbody>
            @for (receipt of data.depositReceipts; track receipt.id) {
              <tr>
                <td class="nowrap">{{ receipt.receivedAt | date: 'dd/MM/yyyy' }}</td>
                <td><b>{{ receipt.roomName }}</b><div class="mute">{{ receipt.propertyName }}</div></td>
                <td>{{ receipt.tenantName }}<div class="mute">{{ receipt.phone || '—' }}</div></td>
                <td class="nowrap">{{ receipt.holdUntil | date: 'dd/MM/yyyy' }}</td>
                <td>@if (receipt.refundedAt) { @if (receipt.refundedAmount) { Hoàn {{ receipt.refundedAmount | currency: 'VND':'symbol':'1.0-0' }} } @else { Không hoàn cọc } ngày {{ receipt.refundedAt | date: 'dd/MM/yyyy' }} } @else if (isHoldExpired(receipt)) { Hết hạn giữ } @else { Đang giữ }</td>
                <td class="n">{{ receipt.amount | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td><button class="s d" title="Xóa phiếu cọc" aria-label="Xóa phiếu cọc" (click)="removeDepositReceipt(receipt)"><app-icon name="trash" /></button></td>
              </tr>
            }
          </tbody>
        </table></div>
      } @else {
        <div class="empty">Chưa có phiếu cọc nào trong tháng này.</div>
      }
      }

      <h2><button type="button" class="section-toggle" [attr.aria-expanded]="!collapsed().monthly" (click)="toggle('monthly')"><span>{{ collapsed().monthly ? '▸' : '▾' }}</span>Khoản chi cố định hàng tháng ({{ data.monthlyExpenses.length }})</button></h2>
      @if (!collapsed().monthly) {
      @if (data.monthlyExpenses.length) {
        <div class="tbl"><table>
          <thead><tr><th>Nội dung</th><th>Người nhận</th><th>Nhà</th><th>Ngày gần nhất</th><th class="n">Số tiền</th><th></th></tr></thead>
          <tbody>
            @for (entry of data.monthlyExpenses; track entry.id) {
              <tr>
                <td><b>{{ entry.description }}</b><div class="mute">{{ categoryLabel(entry.category) }}</div></td>
                <td>{{ entry.recipient || '—' }}</td><td>{{ entry.propertyName || 'Chi phí chung' }}</td>
                <td class="nowrap">{{ entry.date | date: 'dd/MM/yyyy' }}</td>
                <td class="n">{{ entry.amount | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td class="n nowrap">@if (!entry.isPaid) {<button class="s" title="Sửa khoản chi" aria-label="Sửa khoản chi" (click)="edit(entry)"><app-icon name="edit" /></button>
                  <button class="s d" title="Xóa khoản chi" aria-label="Xóa khoản chi" (click)="remove(entry)"><app-icon name="trash" /></button>}</td>
              </tr>
            }
          </tbody>
        </table></div>
      } @else {
        <div class="empty">Chưa có khoản chi cố định hàng tháng nào.</div>
      }
      }

      <h2><button type="button" class="section-toggle" [attr.aria-expanded]="!collapsed().history" (click)="toggle('history')"><span>{{ collapsed().history ? '▸' : '▾' }}</span>Lịch sử lợi nhuận</button></h2>
      @if (!collapsed().history) {
        @if (data.history.length) {
          <div class="tbl"><table>
            <thead><tr><th>Tháng</th><th class="n">Đã thu</th><th class="n">Tổng chi</th><th class="n">Lợi nhuận</th></tr></thead>
            <tbody>
              @for (item of data.history; track item.month) {
                <tr>
                  <td>{{ item.month | monthYear }}</td>
                  <td class="n">{{ item.collected | currency: 'VND':'symbol':'1.0-0' }}</td>
                  <td class="n">{{ item.expenses | currency: 'VND':'symbol':'1.0-0' }}</td>
                  <td class="n"><b>{{ item.profit | currency: 'VND':'symbol':'1.0-0' }}</b></td>
                </tr>
              }
            </tbody>
          </table></div>
        } @else {
          <div class="empty">Chưa có dữ liệu lịch sử.</div>
        }
      }
    }
  `,
  styles: [`
    .financial-form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:0 16px;max-width:840px}
    .check-row{display:flex;align-items:center;gap:8px;font-weight:600;cursor:pointer}
    .check-row input{width:auto;margin:0}
    .recurring-field{display:flex;flex-direction:column;gap:4px;margin:4px 0 12px}
    .recurring-field small{padding-left:24px}
    .section-toggle{all:unset;cursor:pointer;display:inline-flex;align-items:center;gap:8px;font:inherit;color:inherit}
    .section-toggle span{width:14px}
    .section-toggle:focus-visible{outline:2px solid currentColor;outline-offset:4px}
    @media(max-width:720px){.financial-form-grid{grid-template-columns:1fr}.financial-form-grid .span-2{grid-column:auto}}
  `],
})
export class Financials {
  api = inject(Api);
  auth = inject(Auth);
  private alerts = inject(Alerts);
  private editor = viewChild.required<ElementRef<HTMLDialogElement>>('editor');
  report = signal<FinancialReport | null>(null);
  collapsed = signal({ expenses: false, monthly: true, deposits: false, history: true });
  editing = signal(false);
  editId = '';
  form = this.newForm();

  constructor() { effect(() => { this.api.pid(); this.api.month(); this.load(); }); }

  newForm() {
    const propertyId = this.auth?.isEmployee?.() ? this.api?.pid?.() || this.api?.properties?.()[0]?.id || '' : this.api?.pid?.() ?? '';
    return { propertyId, category: 'Operating' as ExpenseCategory,
      description: '', recipient: '', amount: 0, isMonthly: false, isPaid: true, date: `${this.api?.month?.() ?? new Date().toISOString().slice(0, 7)}-01` };
  }

  toggle(section: 'expenses' | 'monthly' | 'deposits' | 'history') { this.collapsed.update(state => ({ ...state, [section]: !state[section] })); }

  load() { this.api.financials().subscribe({ next: result => this.report.set(result), error: error => this.showError(error) }); }

  private showError(error: any) {
    if (error?.status === 404 && !error?.error?.message) {
      this.api.say('API chưa được cập nhật. Hãy khởi động lại backend sau khi xác nhận database để áp dụng migration.');
      return;
    }
    this.api.say(this.api.errorMessage(error));
  }

  isHoldExpired(receipt: DepositReceipt) { return !receipt.refundedAt && receipt.holdUntil < new Date().toLocaleDateString('en-CA'); }

  async removeDepositReceipt(receipt: DepositReceipt) {
    if (!await this.alerts.confirm('Xóa phiếu cọc?', `${receipt.tenantName} - ${receipt.roomName}`, 'Xóa', true)) return;
    this.api.deleteDepositReceipt(receipt.id).subscribe({
      next: () => { this.load(); this.alerts.success('Đã xóa phiếu cọc'); },
      error: error => this.showError(error),
    });
  }

  salesTotal() { return this.report()?.expenseEntries.filter(entry => entry.category === 'Sales' && entry.isPaid).reduce((sum, entry) => sum + entry.amount, 0) ?? 0; }

  categoryLabel(category: ExpenseCategory) {
    return category === 'Sales' ? 'Sale phòng' : category === 'Operating' ? 'Vận hành' : 'Khác';
  }

  add() { this.editId = ''; this.form = this.newForm(); this.editing.set(true); this.editor().nativeElement.showModal(); }

  edit(entry: FinancialExpense) {
    this.editId = entry.id;
    this.form = { propertyId: entry.propertyId ?? '', category: entry.category, description: entry.description,
      recipient: entry.recipient ?? '', amount: entry.amount, date: entry.date, isMonthly: entry.isMonthly, isPaid: entry.isPaid };
    this.editing.set(true);
    this.editor().nativeElement.showModal();
  }

  cancel() { this.editor().nativeElement.close(); this.editing.set(false); }

  togglePaid(entry: FinancialExpense, isPaid: boolean) {
    const body = { propertyId: entry.propertyId || null, category: entry.category, description: entry.description,
      recipient: entry.recipient || null, amount: entry.amount, date: entry.date, isMonthly: entry.isMonthly, isPaid };
    this.api.saveExpense(body, entry.id).subscribe({
      next: () => { this.load(); this.alerts.success(isPaid ? 'Đã đánh dấu đã chi' : 'Đã đánh dấu dự chi'); },
      error: error => { this.load(); this.showError(error); },
    });
  }

  save() {
    if (!this.form.recipient.trim() || !this.form.description.trim() || !this.form.date || this.form.amount <= 0) return this.api.say('Vui lòng nhập đủ người nhận, nội dung, ngày và số tiền hợp lệ');
    const body = { ...this.form, propertyId: this.form.propertyId || null, recipient: this.form.recipient.trim() };
    this.api.saveExpense(body, this.editId || undefined).subscribe({
      next: () => { this.cancel(); this.load(); this.alerts.success(this.editId ? 'Đã cập nhật khoản chi' : 'Đã thêm khoản chi'); },
      error: error => this.showError(error),
    });
  }

  async remove(entry: FinancialExpense) {
    if (!await this.alerts.confirm('Xóa khoản chi?', entry.description, 'Xóa', true)) return;
    this.api.deleteExpense(entry.id).subscribe({
      next: () => { this.load(); this.alerts.success('Đã xóa khoản chi'); },
      error: error => this.showError(error),
    });
  }
}
