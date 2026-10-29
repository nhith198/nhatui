export const cfg = { apiUrl: "http://localhost:5080/api" };  // overridden at startup by /config.json

export interface Property {
  id: string; name: string; address: string; electricRate: number; waterRate: number;
  waterRatePerPerson: number; bankName: string; bankAccountNumber: string; bankAccountName: string;
  serviceAmountPerMonth: number; roomCount: number; occupiedCount: number; canManage: boolean; hasQrCode: boolean;
}
export interface Tenant { tenancyId: string; name: string; phone: string; startDate: string; deposit: number; }
export interface Room {
  id: string; propertyId: string; propertyName: string; name: string; price: number;
  lastElectric: number; lastWater: number; tenant: Tenant | null; isUnderMaintenance: boolean; maintenanceNote: string | null; imageCount: number;
  occupantCount: number; waterByOccupant: boolean; serviceAmount: number; waterRatePerPerson: number; electricRate: number | null;
  reservedTenantName: string | null; reservedPhone: string | null; reservedDeposit: number | null; reservedDate: string | null; reservedUntil: string | null;
}
export type ContractStatus = 'Active' | 'Upcoming' | 'Expiring' | 'Expired' | 'Ended';
export const contractLabels: Record<ContractStatus, string> = { Active: 'Đang hiệu lực', Upcoming: 'Chưa bắt đầu', Expiring: 'Sắp hết hạn', Expired: 'Quá hạn', Ended: 'Đã kết thúc' };
export interface Occupant {
  id: string;
  roomId: string;
  propertyId: string;
  propertyName: string;
  roomName: string;
  name: string;
  phone: string;
  identityNumber: string;
  gender: string;
  temporaryResidenceRegistered: boolean;
  address: string;
  notes: string;
}

export interface Tenancy {
  id: string; roomId: string; propertyId: string; propertyName: string; roomName: string;
  name: string; phone: string; identityNumber: string; gender: string; address: string; notes: string;
  startDate: string; expectedEndDate: string | null; endDate: string | null; deposit: number; status: ContractStatus;
  temporaryResidenceRegistered: boolean;
}
export interface RoomImage { id: string; caption: string; contentType: string; base64: string; }
export interface Employee { id: string; fullName: string; phone: string; isDisabled: boolean; propertyIds: string[]; pagePermissions: string[]; }
export interface EmployeePageOption { key: string; label: string; }
export const employeePageOptions: EmployeePageOption[] = [
  { key: 'dashboard', label: 'Tổng quan' },
  { key: 'rooms', label: 'Phòng' },
  { key: 'tenancies', label: 'Khách thuê & hợp đồng' },
  { key: 'invoices', label: 'Hóa đơn' },
  { key: 'complaints', label: 'Khiếu nại' },
  { key: 'financials', label: 'Tài chính' },
  { key: 'properties', label: 'Nhà' },
  { key: 'repairs', label: 'Yêu cầu sửa chữa' },
];
export interface Assignee { id: string; fullName: string; }
export type ComplaintStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed';
export const complaintLabels: Record<ComplaintStatus, string> = { Open: 'Mới tiếp nhận', InProgress: 'Đang xử lý', Resolved: 'Đã giải quyết', Closed: 'Đã đóng' };
export interface Complaint {
  id: string; roomId: string; propertyId: string; propertyName: string; roomName: string; tenantName: string;
  title: string; description: string; priority: 'Low' | 'Normal' | 'Urgent'; status: ComplaintStatus;
  assignedTo: string | null; assignedName: string | null; resolution: string; createdAt: string; resolvedAt: string | null;
}
export type RepairStatus = 'New' | 'InProgress' | 'Fixed' | 'Closed';
export const repairLabels: Record<RepairStatus, string> = { New: 'Mới tiếp nhận', InProgress: 'Đang xử lý', Fixed: 'Đã sửa xong', Closed: 'Đã đóng' };
export interface RepairRequest {
  roomId: string; propertyId: string; propertyName: string; roomName: string; tenantName: string | null;
  description: string; status: RepairStatus;
}
export interface Invoice {
  id: string; roomId: string; roomName: string; propertyName: string; tenantName: string; gender: string; month: string;
  electricOld: number; electricNew: number; waterOld: number; waterNew: number;
  rent: number; electricAmount: number; waterAmount: number; other: number; total: number; isPaid: boolean;
  serviceAmount: number; waterByOccupant: boolean; occupantCount: number; waterRatePerPerson: number; electricRate: number;
  otherNote: string | null; propertyId: string; propertyHasQr: boolean;
}
export interface Missing { roomId: string; roomName: string; propertyName: string; tenantName: string; }
export interface DashboardData { totalRooms: number; occupiedRooms: number; invoiced: number; collected: number; outstanding: number; missing: Missing[]; }
export interface FinancialExpense {
  id: string; propertyId: string | null; propertyName: string | null; category: 'Operating' | 'Sales' | 'Other';
  description: string; recipient: string | null; amount: number; date: string; isMonthly: boolean; isPaid: boolean; isGenerated: boolean;
}
export interface MonthlyProfit { month: string; collected: number; expenses: number; profit: number; }
export interface DepositReceipt {
  id: string; roomId: string; roomName: string; propertyName: string; tenantName: string; phone: string | null;
  amount: number; holdUntil: string; receivedAt: string; refundedAt: string | null; refundedAmount: number | null;
}
export interface FinancialReport {
  month: string; collected: number; expenses: number; profit: number;
  expenseEntries: FinancialExpense[]; history: MonthlyProfit[]; depositReceipts: DepositReceipt[]; monthlyExpenses: FinancialExpense[];
}
export interface AccountProfile { id: string; fullName: string; phone: string | null; isEmployee: boolean; pagePermissions: string[]; }
