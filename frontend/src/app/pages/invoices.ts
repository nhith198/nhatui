import { Component, effect, inject, signal } from '@angular/core';
import { Api } from '../core/api';
import { Invoice } from '../core/models';
import { InvoiceTable } from '../core/invoice-table';
import { MonthYearPipe } from '../core/month-year';

@Component({
  selector: 'app-invoices',
  imports: [InvoiceTable, MonthYearPipe],
  template: `
    <h2 class="first">Hóa đơn tháng {{ api.month() | monthYear }}</h2>
    <app-invoice-table [list]="list()" empty="Chưa có hóa đơn nào trong tháng này. Vào tab Phòng để lập hóa đơn." (changed)="load()" />
  `,
})
export class Invoices {
  api = inject(Api);
  list = signal<Invoice[]>([]);
  constructor() { effect(() => { this.api.pid(); this.api.month(); this.load(); }); }
  load() { this.api.invoices().subscribe(l => this.list.set(l)); }
}
