# Quản lý nhà trọ

Hệ thống cho chủ nhà quản lý nhiều nhà cho thuê: nhà, phòng, khách thuê, hóa đơn điện nước hàng tháng và thu tiền.

| Lớp | Công nghệ |
|---|---|
| Frontend | Angular 22 (standalone, signals, lazy routes) |
| Backend | ASP.NET Core Web API, .NET 10, EF Core 10, JWT |
| Database | SQL Server 2022 / Azure SQL |
| CI/CD | Azure DevOps Pipelines |

## Kiến trúc (Clean Architecture)

```
backend/
  NhaTro.slnx
  src/
    NhaTro.Domain/          Lớp trong cùng, không phụ thuộc gì: Room, Property, Invoice, Tenancy, User + luật nghiệp vụ
    NhaTro.Application/     Use case (command/query handler), DTO, các cổng (interface) do chính lớp này định nghĩa
    NhaTro.Infrastructure/  Cài đặt các cổng: EF Core + SQL Server, repository, truy vấn đọc, JWT, băm mật khẩu
    NhaTro.Api/             Controller mỏng, request contract, middleware lỗi, composition root (Program.cs)
  tests/
    NhaTro.Domain.Tests/          Test thuần luật nghiệp vụ, không cần database
    NhaTro.Application.Tests/     Test handler với fake repository
    NhaTro.ArchitectureTests/     Canh quy tắc phụ thuộc (NetArchTest)
    NhaTro.Infrastructure.Tests/  Test phân quyền theo nhà, lưu trữ và HTTP API với database trong bộ nhớ
frontend/                        Angular
pipelines/templates/deploy.yml   Template deploy dùng cho Dev và Prod
azure-pipelines.yml
docker-compose.yml
```

**Quy tắc phụ thuộc:** `Api -> Infrastructure -> Application -> Domain`. Mũi tên chỉ hướng vào trong; Domain và Application không biết EF Core, ASP.NET hay SQL Server. Application khai báo cổng (`IRoomRepository`, `IUnitOfWork`, `ITokenService`, `IPasswordHasher`, `IInvoiceQueries`...), Infrastructure cài đặt, Program.cs nối chúng lại bằng DI. `NhaTro.ArchitectureTests` làm test fail nếu ai đó vi phạm quy tắc này.

**Domain (mô hình giàu hành vi):**
- Thuộc tính chỉ có `private set`; thay đổi trạng thái đi qua phương thức: `Room.MoveIn`, `Room.MoveOut`, `Room.IssueInvoice`, `Invoice.MarkPaid`...
- `Room` là aggregate root của `Tenancy`; chỉ `Room.IssueInvoice` tạo được `Invoice`, nên việc tính tiền và cập nhật chỉ số công tơ không thể bị bỏ qua.
- Các aggregate (`Property`, `Room`, `Invoice`, `User`) tham chiếu nhau bằng Id, không bằng object.
- Vi phạm luật ném `DomainException` (Validation hoặc Conflict).

**Application (CQRS nhẹ):**
- Command (ghi): handler nạp aggregate qua repository, gọi hành vi của domain, rồi `IUnitOfWork.SaveChangesAsync`.
- Query (đọc): `IPropertyQueries`, `IRoomQueries`... chiếu thẳng ra DTO, không nạp aggregate.
- Handler tự động đăng ký vào DI bằng cách quét assembly.
- Lỗi: `NotFoundException`, `ConflictException`, `UnauthorizedException`; `ErrorHandlingMiddleware` đổi cả lỗi này và `DomainException` thành HTTP 404/409/401/400 kèm `{ "message": ... }`.

**Cách thêm một tính năng:** thêm hành vi vào entity (Domain) -> thêm command/handler (Application) -> nếu cần dữ liệu mới thì thêm cổng và cài đặt (Infrastructure) -> thêm endpoint (Api).

## Chạy local

```bash
docker compose up -d                                  # SQL Server
dotnet run --project backend/src/NhaTro.Api           # API: http://localhost:5080
cd frontend && npm install && npm start               # Web: http://localhost:4200
dotnet test backend/NhaTro.slnx                       # Domain, Application và Architecture test
```

Ở môi trường Development, API tự áp migration nếu có, nếu chưa có migration thì tạo schema trực tiếp.

## Quản lý vận hành

- **Nhà:** lưu giá điện, giá nước theo m³/đầu người, phí dịch vụ mặc định theo phòng/tháng và thông tin tài khoản ngân hàng nhận tiền.
- **Phòng:** lưu số người ở, cách tính nước theo công tơ hoặc đầu người, phí dịch vụ; lọc theo tên hoặc trạng thái Trống / Đang thuê / Bảo trì. Hóa đơn theo đầu người không cần nhập chỉ số nước và lưu snapshot đơn giá/số người/phí để lịch sử không đổi. Phòng bảo trì không được nhận khách; phòng đang thuê phải trả phòng trước khi bảo trì.
- **Khách thuê & hợp đồng:** danh sách theo nhà, tìm tên/điện thoại/phòng; chỉnh sửa CCCD/hộ chiếu, liên hệ, địa chỉ, ghi chú và ngày hết hạn. Giữ lịch sử sau khi trả phòng.
- **Trạng thái hợp đồng:** Chưa bắt đầu, Đang hiệu lực, Sắp hết hạn (trong 30 ngày), Quá hạn, Đã kết thúc. Hết hạn dự kiến không tự trả phòng; chỉ thao tác Trả phòng kết thúc kỳ thuê và giải phóng phòng.
- **Ảnh phòng:** tải lên, xem lớn và xóa; JPEG/PNG/WebP tối đa 2 MB/ảnh, 8 ảnh/phòng. Ảnh lưu trong SQL Server và chỉ trả về sau khi xác thực quyền truy cập phòng.
- **Khiếu nại:** chủ nhà/nhân viên nhập theo phòng, lưu người phản ánh, ưu tiên, người xử lý và kết quả. Luồng Mới tiếp nhận / Đang xử lý / Đã giải quyết / Đã đóng; bắt buộc có kết quả khi giải quyết hoặc đóng, có thể mở lại.
- **Nhân viên:** chủ nhà tạo tài khoản, phân công nhà và chọn các trang được phép truy cập. Máy chủ kiểm tra quyền khi tải trang/API; nhân viên không thể tự cấp quyền, quản lý nhân viên, hoặc thêm/sửa/xóa nhà/phòng. Tài chính được giới hạn ở nhà được phân công; chi phí chung chỉ chủ nhà xem. Gỡ phân công hoặc khóa tài khoản có hiệu lực ngay trên máy chủ.
- **Tài chính:** theo dõi tiền hóa đơn đã thu theo ngày thanh toán, chi phí vận hành và chi phí sale phòng theo tháng; xem lợi nhuận 12 tháng và ghi/sửa/xóa khoản chi gắn với nhà hoặc chi phí chung.

Thanh chọn nhà áp dụng chung cho các trang được cấp quyền. Xóa phòng sẽ xóa cả lịch sử thuê, hóa đơn, ảnh và khiếu nại liên quan.

## Migration

Đã có `InitialCreate`, `PropertyManagement`, `PhoneAuthentication`, `FinancialExpenses` và `BillingAndEmployeePagePermissions`; không tạo lại migration cùng tên. Để cập nhật database, chạy từ thư mục gốc repo:

```powershell
cd backend
dotnet tool restore
dotnet ef database update --configuration Release --project src/NhaTro.Infrastructure --startup-project src/NhaTro.Api
```

Lệnh trên thay đổi schema của database trong `ConnectionStrings:Default`; kiểm tra đúng server và tài khoản trước khi chạy. Migration mới chỉ bổ sung bảng/cột, không xóa dữ liệu hiện có. Dừng và chạy lại API để nạp phiên bản mới. Không cần sửa các file migration đã sinh.

Khi thay đổi model sau này, tạo migration với tên mới:

```powershell
dotnet ef migrations add TenMoTaThayDoi --project src/NhaTro.Infrastructure --startup-project src/NhaTro.Api
```

Commit thư mục `Migrations`. Pipeline tạo `efbundle` và chạy nó trước khi deploy API. Nếu API Debug đang giữ khóa DLL, dùng `--configuration Release` cho lệnh EF hoặc dừng API trước khi build lại.

Frontend chạy mặc định tại `http://localhost:4200`. Khi cổng này bận, có thể chạy `npm.cmd start -- --host 127.0.0.1 --port 4201` trong thư mục `frontend`; origin này chỉ được cho phép thêm ở môi trường Development.

## CI/CD trên Azure DevOps

Luồng: **Build** (build, test, publish API, bundle migration, build Angular) -> **Dev** (tự động từ `main`) -> **Prod** (sau khi được duyệt).

Thiết lập một lần:

1. **Service connection** kiểu Azure Resource Manager cho mỗi môi trường: `sc-nhatro-dev`, `sc-nhatro-prod`.
2. **Environments**: `nhatro-dev`, `nhatro-prod`. Thêm *Approvals and checks* cho `nhatro-prod`.
3. **Library > Variable groups** `nhatro-dev`, `nhatro-prod`, mỗi nhóm có:
   - `SqlConnectionString` (secret): chuỗi kết nối Azure SQL dùng để chạy migration.
   - `apiUrl`: ví dụ `https://app-nhatro-api-dev.azurewebsites.net/api`.
4. **Tài nguyên Azure** (đổi tên trong `azure-pipelines.yml` cho khớp): App Service Linux (.NET 10), Storage Account bật static website, Azure SQL.
5. **App settings của App Service** (nên dùng Key Vault reference):
   `ConnectionStrings__Default`, `Jwt__Key` (ít nhất 32 ký tự ngẫu nhiên), `Jwt__Issuer`, `Jwt__Audience`, `Cors__Origins__0` (URL static website của web).
6. Service principal của service connection cần quyền **Storage Blob Data Contributor** trên storage account. Firewall Azure SQL cần cho phép agent chạy migration.

Frontend dùng một bản build cho mọi môi trường: pipeline ghi `config.json` (`apiUrl`) vào bản deploy; khi chạy local file này trỏ tới `http://localhost:5080/api`.

## Mô hình dữ liệu

`User` 1-n `Property` 1-n `Room` 1-n `Tenancy` và `Invoice`.

- Mọi truy vấn lọc theo `Property.OwnerId` của người đăng nhập.
- `Tenancy.EndDate = null` nghĩa là đang thuê; index unique có filter đảm bảo mỗi phòng một hợp đồng đang hiệu lực.
- `Tenancy.ExpectedEndDate` là hạn dự kiến, khác với `EndDate` thực tế trả phòng; hồ sơ khách lưu trên từng kỳ thuê.
- `User.EmployerId` xác định nhân viên; `StaffAssignments` giới hạn nhà được truy cập và `EmployeePagePermissions` giới hạn trang. Chủ nhà là người duy nhất được sửa quyền nhân viên.
- `RoomImages` lưu dữ liệu ảnh theo phòng; `Complaints` lưu lịch sử tiếp nhận/xử lý và nhân viên được giao.
- `Invoice` unique `(RoomId, Month)`; tên khách lưu snapshot khi lập hóa đơn.
- `Room.LastElectric/LastWater` giữ chỉ số công tơ gần nhất, hóa đơn mới lấy làm chỉ số cũ.

## API

| Method | Đường dẫn | Mô tả |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | Trả về JWT |
| GET/POST/PUT/DELETE | `/api/properties` | Giá điện, nước theo m³/đầu người và thông tin ngân hàng nhận tiền |
| GET/POST/PUT/DELETE | `/api/rooms` | `?propertyId=` để lọc; số người, chế độ tính nước và phí dịch vụ |
| POST | `/api/rooms/{id}/rent`, `/checkout` | Cho thuê, trả phòng |
| GET/POST | `/api/invoices` | `?month=yyyy-MM&propertyId=` |
| POST | `/api/invoices/{id}/toggle-paid` | Đánh dấu đã thu |
| GET | `/api/dashboard` | `?month=&propertyId=` |
| GET | `/api/financials` | `?month=yyyy-MM&propertyId=`; doanh thu theo ngày thanh toán, chi phí và lợi nhuận 12 tháng; nhân viên cần quyền Tài chính |
| POST/PUT/DELETE | `/api/financials/expenses` | Ghi, sửa, xóa khoản chi; nhóm `Operating`, `Sales`, `Other`; nhân viên chỉ thao tác trên nhà được giao |

Các API bổ sung:

- `GET /api/account`: hồ sơ và vai trò tài khoản hiện tại.
- `GET /api/tenancies?propertyId=`: hồ sơ và lịch sử hợp đồng theo nhà.
- `PUT /api/tenancies/{id}/rooms/{roomId}`: cập nhật hồ sơ và hạn hợp đồng.
- `PUT /api/rooms/{id}/maintenance`: bật/tắt bảo trì (`enabled`).
- `GET/POST /api/rooms/{roomId}/images`, `GET/DELETE /api/rooms/{roomId}/images/{id}`: ảnh phòng; POST multipart gồm `file` và `caption`.
- `GET/POST /api/complaints`, `GET/PUT /api/complaints/{id}`: khiếu nại; GET hỗ trợ `propertyId`.
- `GET /api/complaints/assignees?propertyId=`: người có thể nhận xử lý tại nhà.
- `GET/POST /api/employees`, `GET/PUT /api/employees/{id}`: nhân viên, phân công nhà và `PagePermissions`, chỉ chủ nhà.

API trả HTTP 403 khi tài khoản đã đăng nhập nhưng không có quyền thao tác; tài khoản bị khóa trả 401, tài nguyên ngoài nhà được giao trả 404.
