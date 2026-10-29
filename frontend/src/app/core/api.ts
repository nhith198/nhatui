import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { AccountProfile, cfg, DashboardData, FinancialExpense, FinancialReport, Invoice, Property, Room, Tenancy, Employee, Complaint, Assignee, RoomImage, Occupant, RepairRequest } from './models';

/** Shared UI state (selected property and month) plus the HTTP client for the API. */
@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  properties = signal<Property[]>([]);
  pid = signal('');
  month = signal(new Date().toISOString().slice(0, 7));
  toast = signal('');
  private toastTimer: ReturnType<typeof setTimeout> | undefined;

  say(msg: string) {
    this.toast.set(msg);
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(''), 2500);
  }

  errorMessage(e: any): string {
    return e?.error?.message ?? e?.error?.title ?? 'Có lỗi xảy ra, vui lòng thử lại';
  }

  private q(extra: Record<string, string> = {}) {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(extra)) if (v) p = p.set(k, v);
    return p;
  }

  loadProperties() {
    this.http.get<Property[]>(`${cfg.apiUrl}/properties`).subscribe({
      next: p => { this.properties.set(p); if (this.pid() && !p.some(property => property.id === this.pid())) this.pid.set(''); },
      error: e => { if (e?.status !== 403) this.say(this.errorMessage(e)); },
    });
  }
  account() { return this.http.get<AccountProfile>(`${cfg.apiUrl}/account`); }
  updateAccount(body: unknown) { return this.http.put<AccountProfile>(`${cfg.apiUrl}/account`, body); }
  saveProperty(body: unknown, id?: string) {
    return id ? this.http.put<{ id: string }>(`${cfg.apiUrl}/properties/${id}`, body) : this.http.post<{ id: string }>(`${cfg.apiUrl}/properties`, body);
  }
  deleteProperty(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/properties/${id}`); }
  propertyQr(id: string) { return this.http.get(`${cfg.apiUrl}/properties/${id}/qr`, { responseType: 'blob' }); }
  uploadPropertyQr(id: string, file: File) {
    const form = new FormData(); form.append('file', file);
    return this.http.put<void>(`${cfg.apiUrl}/properties/${id}/qr`, form);
  }
  deletePropertyQr(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/properties/${id}/qr`); }

  rooms() { return this.http.get<Room[]>(`${cfg.apiUrl}/rooms`, { params: this.q({ propertyId: this.pid() }) }); }
  addRoom(body: unknown) { return this.http.post<void>(`${cfg.apiUrl}/rooms`, body); }
  updateRoom(id: string, body: unknown) { return this.http.put<void>(`${cfg.apiUrl}/rooms/${id}`, body); }
  deleteRoom(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/rooms/${id}`); }
  rent(id: string, body: unknown) { return this.http.post<void>(`${cfg.apiUrl}/rooms/${id}/rent`, body); }
  checkout(id: string) { return this.http.post<void>(`${cfg.apiUrl}/rooms/${id}/checkout`, {}); }
  reserve(id: string, body: unknown) { return this.http.post<void>(`${cfg.apiUrl}/rooms/${id}/reserve`, body); }
  cancelReservation(id: string, refundAmount: number) { return this.http.post<void>(`${cfg.apiUrl}/rooms/${id}/cancel-reservation`, { refundAmount }); }
  maintenance(id: string, enabled: boolean, damagedEquipment: string | null = null) { return this.http.put<void>(`${cfg.apiUrl}/rooms/${id}/maintenance`, { enabled, damagedEquipment }); }
  images(id: string) { return this.http.get<RoomImage[]>(`${cfg.apiUrl}/rooms/${id}/images`); }
  uploadImage(id: string, file: File, caption: string) {
    const form = new FormData(); form.append('file', file); form.append('caption', caption);
    return this.http.post<{ id: string }>(`${cfg.apiUrl}/rooms/${id}/images`, form);
  }
  deleteImage(roomId: string, id: string) { return this.http.delete<void>(`${cfg.apiUrl}/rooms/${roomId}/images/${id}`); }
  tenancies() { return this.http.get<Tenancy[]>(`${cfg.apiUrl}/tenancies`, { params: this.q({ propertyId: this.pid() }) }); }
  updateTenant(tenant: Tenancy, body: unknown) { return this.http.put<void>(`${cfg.apiUrl}/tenancies/${tenant.id}/rooms/${tenant.roomId}`, body); }
  occupants() { return this.http.get<Occupant[]>(`${cfg.apiUrl}/occupants`, { params: this.q({ propertyId: this.pid() }) }); }
  saveOccupant(body: unknown, id?: string) {
    return this.http.request<{ id: string } | null>(id ? 'PUT' : 'POST', id ? `${cfg.apiUrl}/occupants/${id}` : `${cfg.apiUrl}/occupants`, { body });
  }
  deleteOccupant(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/occupants/${id}`); }
  employees() { return this.http.get<Employee[]>(`${cfg.apiUrl}/employees`); }
  deleteEmployee(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/employees/${id}`); }
  saveEmployee(body: unknown, id?: string) {
    return id ? this.http.put<void>(`${cfg.apiUrl}/employees/${id}`, body) : this.http.post<void>(`${cfg.apiUrl}/employees`, body);
  }
  complaints() { return this.http.get<Complaint[]>(`${cfg.apiUrl}/complaints`, { params: this.q({ propertyId: this.pid() }) }); }
  assignees(propertyId: string) { return this.http.get<Assignee[]>(`${cfg.apiUrl}/complaints/assignees`, { params: this.q({ propertyId }) }); }
  addComplaint(body: unknown) { return this.http.post<void>(`${cfg.apiUrl}/complaints`, body); }
  updateComplaint(id: string, body: unknown) { return this.http.put<void>(`${cfg.apiUrl}/complaints/${id}`, body); }
  deleteComplaint(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/complaints/${id}`); }
  repairs() { return this.http.get<RepairRequest[]>(`${cfg.apiUrl}/repairs`, { params: this.q({ propertyId: this.pid() }) }); }
  updateRepair(roomId: string, status: string) { return this.http.put<void>(`${cfg.apiUrl}/repairs/${roomId}/status`, { status }); }

  invoices() { return this.http.get<Invoice[]>(`${cfg.apiUrl}/invoices`, { params: this.q({ month: this.month(), propertyId: this.pid() }) }); }
  createInvoice(body: unknown) { return this.http.post<Invoice>(`${cfg.apiUrl}/invoices`, body); }
  togglePaid(id: string) { return this.http.post<void>(`${cfg.apiUrl}/invoices/${id}/toggle-paid`, {}); }
  deleteInvoice(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/invoices/${id}`); }

  dashboard() { return this.http.get<DashboardData>(`${cfg.apiUrl}/dashboard`, { params: this.q({ month: this.month(), propertyId: this.pid() }) }); }
  financials() { return this.http.get<FinancialReport>(`${cfg.apiUrl}/financials`, { params: this.q({ month: this.month(), propertyId: this.pid() }) }); }
  saveExpense(body: unknown, id?: string) {
    return id ? this.http.put<void>(`${cfg.apiUrl}/financials/expenses/${id}`, body)
      : this.http.post<void>(`${cfg.apiUrl}/financials/expenses`, body);
  }
  deleteExpense(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/financials/expenses/${id}`); }
  deleteDepositReceipt(id: string) { return this.http.delete<void>(`${cfg.apiUrl}/financials/deposits/${id}`); }
}
