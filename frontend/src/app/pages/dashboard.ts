import { CurrencyPipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { DashboardData, Invoice } from '../core/models';
import { InvoiceTable } from '../core/invoice-table';
import { MonthYearPipe } from '../core/month-year';

@Component({
  selector: 'app-dashboard',
  imports: [CurrencyPipe, RouterLink, InvoiceTable, MonthYearPipe],
  template: `
    @if (data(); as d) {
      <div class="stats">
        <div class="stat"><b>{{ d.occupiedRooms }}/{{ d.totalRooms }}</b><span>Phòng đang thuê</span></div>
        <div class="stat"><b>{{ d.invoiced | currency: 'VND':'symbol':'1.0-0' }}</b><span>Tổng hóa đơn tháng {{ api.month() | monthYear }}</span></div>
        <div class="stat"><b>{{ d.collected | currency: 'VND':'symbol':'1.0-0' }}</b><span>Đã thu</span></div>
        <div class="stat"><b>{{ d.outstanding | currency: 'VND':'symbol':'1.0-0' }}</b><span>Còn phải thu</span></div>
      </div>
      <h2>Chưa lập hóa đơn ({{ d.missing.length }})</h2>
      @if (d.missing.length) {
        <div class="tbl"><table><tbody>
          @for (m of d.missing; track m.roomId) {
            <tr><td><b>{{ m.roomName }}</b><div class="mute">{{ m.propertyName }}</div></td><td>{{ m.tenantName }}</td>
                <td class="n"><a routerLink="/rooms" class="btn p s">Lập hóa đơn</a></td></tr>
          }
        </tbody></table></div>
      } @else {
        <div class="card empty">Mọi phòng đang thuê đều đã có hóa đơn tháng này.</div>
      }
    }
    <h2>Chưa thu tiền ({{ unpaid().length }})</h2>
    <app-invoice-table [list]="unpaid()" empty="Không có khoản nào cần thu." (changed)="load()" />
  `,
})
export class Dashboard {
  api = inject(Api);
  data = signal<DashboardData | null>(null);
  unpaid = signal<Invoice[]>([]);

  constructor() {
    effect(() => { this.api.pid(); this.api.month(); this.load(); });
  }

  load() {
    this.api.dashboard().subscribe(d => this.data.set(d));
    this.api.invoices().subscribe(l => this.unpaid.set(l.filter(i => !i.isPaid)));
  }
}
