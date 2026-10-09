# HIỂU ĐỒ ÁN QUẢN LÝ SINH VIÊN – TỔNG HỢP TUẦN 1 → 7

> Tài liệu này trả lời 2 câu: **đồ án đang làm gì** và **vì sao làm như thế**.
> Nguồn: file `02_Angular_tutorial.pdf`, yêu cầu mentor (Tuan02.docx, Tuan03-4.docx, tuần 5-6, tuần 7) và **code thật** trong dự án.
>
> Mỗi yêu cầu được trình bày cùng một khuôn:
> **Khái niệm → Bản chất (vì sao cần) → Cách hoạt động trong dự án → Cách thấy trên web → Keyword**

---

## MỤC LỤC

0. [Kết quả chạy dự án hôm nay](#0-kết-quả-chạy-dự-án-hôm-nay)
1. [Bức tranh tổng thể](#1-bức-tranh-tổng-thể)
2. [Angular nền tảng (theo file PDF)](#2-angular-nền-tảng-theo-file-pdf)
3. [Tuần 1 – Nền tảng & bảo mật](#3-tuần-1--nền-tảng--bảo-mật)
4. [Tuần 2 – Tính năng chính](#4-tuần-2--tính-năng-chính)
5. [Tuần 3-4 – Dứt điểm Exception, Audit, quản lý file rác](#5-tuần-3-4--dứt-điểm-exception-audit-quản-lý-file-rác)
6. [Tuần 5-6 – Xóa mềm & Unit Test](#6-tuần-5-6--xóa-mềm--unit-test)
7. [Tuần 7 – Integration Test, Coverage, Concurrency](#7-tuần-7--integration-test-coverage-concurrency)
8. [Bảng keyword tổng hợp](#8-bảng-keyword-tổng-hợp)
9. [Cách tự chạy lại](#9-cách-tự-chạy-lại)

---

## 0. Kết quả chạy dự án hôm nay

| Thành phần | Địa chỉ | Kết quả |
|---|---|---|
| SQL Server | `MSI\SQLEXPRESS`, DB `QLSINHVIEN` | ✅ Đang chạy |
| Backend ASP.NET Core (.NET 10) | `https://localhost:7280` | ✅ Lên, trả JSON đúng |
| Frontend Angular 22 | `http://localhost:4200` | ✅ Đăng nhập, xem danh sách được |
| Test (xUnit + TestContainers) | `dotnet test` | ✅ **32/32 pass** (54 giây) |
| Code Coverage tầng Service | `SinhVienService` | ✅ **100% dòng** (yêu cầu ≥ 80%) |

Các kịch bản đã kiểm chứng bằng gọi API thật:

| Kịch bản | Kết quả trả về | Yêu cầu tuần |
|---|---|---|
| Đăng ký tài khoản | `{"message":"Đăng ký thành công!"}` | T1 |
| Xem DB cột `PasswordHash` | `$2a$11$3Llz...` (chuỗi băm BCrypt) | T1 |
| Đăng nhập sai mật khẩu | `400` `{statusCode, message, details}` | T2, T3-4 |
| Gọi API không có token | `401` "Bạn chưa đăng nhập..." | T1, T2 |
| Danh sách trang 1, 3 dòng, sắp tuổi giảm | `items` 3 dòng, `totalCount: 8`, `totalPages: 3` | T2 |
| Giảng viên bấm Thêm sinh viên | `403` "Bạn không có quyền..." | T2 |
| Lấy sinh viên Id 99999 | `404` "Không tìm thấy sinh viên..." | T3-4 |
| Giảng viên xem Lịch sử | `403` + giao diện bị Guard chặn, hiện Toast | T2 |
| Trong DB: 19 sinh viên, 11 dòng `IsDeleted = 1` | Xóa mềm hoạt động | T5-6 |

> ⚠️ Để chạy thử, mình đã tạo một tài khoản kiểm thử `demo_claude` (quyền GiangVien) trong DB. Muốn xóa:
> `DELETE FROM Users WHERE Username = 'demo_claude';`

---

## 1. Bức tranh tổng thể

### 1.1 Đồ án làm gì?

Một web **quản lý sinh viên** có đăng nhập và phân quyền:
- **Admin**: thêm / sửa / xóa sinh viên, đổi ảnh, xem lịch sử hoạt động.
- **GiangVien**: chỉ xem danh sách và đổi ảnh đại diện.

Dự án chia **3 tầng tách biệt**, mỗi tầng chạy riêng:

```
┌──────────────────────────────┐
│  ANGULAR (localhost:4200)    │  Giao diện – chạy trong trình duyệt
│  Component → Service         │
│  → jwtInterceptor (gắn token)│
│  → errorInterceptor (toast)  │
└──────────────┬───────────────┘
               │  HTTP + JSON
               ▼
┌──────────────────────────────┐
│  ASP.NET CORE API (7280)     │  Xử lý nghiệp vụ, bảo mật
│  ExceptionMiddleware         │
│  → CORS → StaticFiles        │
│  → Authentication (JWT)      │
│  → Authorization (Role)      │
│  → Controller → Service      │
│  → AppDbContext (EF Core)    │
│     • Query Filter (xóa mềm) │
│     • Audit Interceptor      │
└──────────────┬───────────────┘
               │  SQL
               ▼
┌──────────────────────────────┐
│  SQL SERVER – QLSINHVIEN     │  SinhVien · Users · AuditLogs
└──────────────────────────────┘
```

### 1.2 Vì sao tách 3 tầng?

| Lý do | Giải thích |
|---|---|
| **Bảo mật** | Trình duyệt không bao giờ chạm thẳng vào DB. Mọi thứ phải đi qua API có kiểm tra token, quyền. |
| **Thay thế độc lập** | Có thể làm thêm app mobile gọi cùng API mà không sửa backend. |
| **Phân công** | Frontend lo hiển thị, backend lo nghiệp vụ – sửa chỗ này không vỡ chỗ kia. |

### 1.3 Hành trình của 1 request – ví dụ "Admin bấm Xóa sinh viên số 5"

```
1. Người dùng bấm nút Xóa  →  (click)="xoa(sv.id)"            [Event binding]
2. Component gọi sinhVienService.delete(5)                    [Service]
3. HttpClient tạo request DELETE /api/SinhVien/5
4. jwtInterceptor chèn header  Authorization: Bearer <token>  [Tuần 1]
5. API: ExceptionMiddleware bọc try/catch toàn bộ phía sau    [Tuần 2-4]
6. API: Authentication đọc token → biết là "admin", Role=Admin
7. API: [Authorize(Roles="Admin")] → cho qua                  [Tuần 2]
8. SinhVienController.Delete → SinhVienService.DeleteAsync    [Tuần 5-6]
9. Service đặt sv.IsDeleted = true  (không DELETE thật)       [Tuần 5-6]
10. SaveChangesAsync → AuditInterceptor chèn 1 dòng AuditLogs [Tuần 3-4]
11. SQL: UPDATE SinhVien SET IsDeleted=1 ... + INSERT AuditLogs
12. API trả 204 No Content
13. Angular: toast "Xóa thành công!" và tải lại danh sách
    (Nếu lỗi bất kỳ → errorInterceptor hiện toast đỏ)        [Tuần 2]
```

---

## 2. Angular nền tảng (theo file PDF)

> **Lưu ý quan trọng:** PDF viết cho **Angular 9** (dùng `NgModule`, `app.module.ts`). Dự án của bạn dùng **Angular 22** – kiểu **standalone** (không còn `app.module.ts`). Ý tưởng giống nhau, chỉ khác chỗ khai báo. Bảng dưới đối chiếu từng phần.

### 2.1 Angular CLI (PDF Phần 1)

**Khái niệm:** Công cụ dòng lệnh `ng` để tạo dự án, tạo component/service, chạy và build.

| Lệnh | Tác dụng | Trong dự án |
|---|---|---|
| `npm install -g @angular/cli` | Cài CLI toàn máy | Đã cài |
| `ng new <tên>` | Tạo dự án mới | Đã tạo `QuanLySinhVienAngular` |
| `ng serve` (`-o` mở trình duyệt) | Biên dịch + chạy server dev ở cổng **4200** | `npm start` |
| `ng generate component x` | Tạo component | Đã tạo `sinh-vien`, `login`, `lich-su`... |
| `ng build` | Đóng gói ra thư mục `dist/` để triển khai | Có thư mục `dist/` |

**Cấu trúc dự án:**

| File/thư mục | Vai trò | Trong dự án |
|---|---|---|
| `package.json` | Danh sách thư viện | Angular 22, rxjs, express (SSR), vitest |
| `angular.json` | Cấu hình CLI (entry point, styles...) | Có |
| `node_modules/` | Thư viện tải về bằng npm | Có – không đưa lên git |
| `src/index.html` | Trang HTML duy nhất | Chỉ có `<app-root>` |
| `src/main.ts` | **Entry point** – điểm khởi động | `bootstrapApplication(App, appConfig)` |
| `src/app/` | Code của ứng dụng | Component, service, guard, interceptor |

### 2.2 Cơ chế hoạt động – Angular khởi động thế nào (PDF Phần 2)

**PDF (Angular 9):**
```
index.html → main.ts → AppModule (@NgModule) → AppComponent → app.component.html
```

**Dự án (Angular 22 – standalone):**
```
index.html  ──  <app-root></app-root>         (thẻ rỗng)
   │
main.ts     ──  bootstrapApplication(App, appConfig)
   │
app.config.ts ─ providers: provideRouter(routes),
   │                       provideHttpClient(withInterceptors([jwt, error]))
   │            (thay cho phần imports/providers trong @NgModule)
   ▼
App (app.ts, selector 'app-root')
   template:  <app-toast></app-toast>
              <router-outlet></router-outlet>   ← tùy URL, vẽ component tương ứng
   ▼
/login → LoginComponent   /sinh-vien → SinhVienComponent   /lich-su → LichSuComponent
```

**Bản chất:** Angular là **SPA (Single Page Application)** – trình duyệt chỉ tải **1 trang HTML**, sau đó JavaScript tự vẽ và thay đổi nội dung. Chuyển trang `/login` → `/sinh-vien` **không tải lại trang**, Router chỉ đổi component bên trong `<router-outlet>`.

**Khi `ng build`**, Angular biên dịch TypeScript → JavaScript rồi tự chèn các file `.js` (runtime, polyfills, main...) vào `index.html`. Vì vậy file `index.html` gốc không có thẻ `<script>` nào.

| PDF (NgModule) | Dự án (standalone) |
|---|---|
| `declarations: [AppComponent]` | Mỗi component tự ghi `standalone: true` |
| `imports: [BrowserModule, FormsModule]` | Mỗi component tự `imports: [CommonModule, FormsModule]` |
| `providers: []` | `app.config.ts` → `providers: [...]` |
| `bootstrap: [AppComponent]` | `bootstrapApplication(App, ...)` trong `main.ts` |

### 2.3 Component

**Khái niệm:** Một mảnh giao diện gồm 3 phần: **class `.ts`** (dữ liệu + hàm) – **template `.html`** (hiển thị) – **`.css`** (trang trí).

```ts
@Component({
  selector: 'app-sinh-vien',          // tên thẻ để nhúng
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sinh-vien.html',
  styleUrl: './sinh-vien.css'
})
export class SinhVienComponent implements OnInit { ... }
```

PDF ví class component "giống Controller bên Spring" – nghĩa là nó **nhận thao tác người dùng và quyết định làm gì**, còn việc gọi API được giao cho **Service**.

### 2.4 Data Binding (PDF Phần 3) – đồng bộ dữ liệu giữa class và giao diện

| Loại | Cú pháp | Chiều | Ví dụ thật trong dự án |
|---|---|---|---|
| **Interpolation** | `{{ biến }}` | class → view | `{{ sv.hoTen }}`, `Trang {{ trangHienTai }} / {{ tongSoTrang }}` |
| **Property binding** | `[thuộc tính]="biểu thức"` | class → view | `[disabled]="trangHienTai === 1"`, `[src]="..."`, `[style.width.%]="tienTrinhUpload"` |
| **Event binding** | `(sự kiện)="hàm()"` | view → class | `(click)="xoa(sv.id)"`, `(change)="chonFileChoForm($event)"` |
| **Two-way binding** | `[(ngModel)]="biến"` | 2 chiều | `[(ngModel)]="sinhVien.hoTen"`, `[(ngModel)]="tuKhoaTimKiem"` |

**Bản chất của `[(ngModel)]`:** là viết tắt của `[ngModel]="x"` + `(ngModelChange)="x = $event"` – "hộp chuối trong ngoặc" (banana in a box). Cần import `FormsModule`. Dùng nhiều nhất trong **form** (đúng như PDF nói).

**Cách thấy trên web:** gõ vào ô "Họ tên" → biến `sinhVien.hoTen` trong class đổi ngay; bấm "Hủy bỏ" → `lamMoiForm()` đặt lại biến → ô nhập tự trống.

### 2.5 Structural Directive – `*ngFor`, `*ngIf`, `ngSwitch` (PDF Phần 5, 6, 7)

Dấu `*` = directive **thêm/bớt phần tử khỏi DOM**.

**`*ngFor` – lặp:**
```html
<tr *ngFor="let sv of danhSachSinhVien; let i = index">
  <td>{{ (trangHienTai - 1) * soDongMoiTrang + i + 1 }}</td>   <!-- STT liên tục qua các trang -->
```
- `let i = index` lấy số thứ tự (PDF mục 4). Ngoài ra còn `odd`, `even`, `first`, `last` (PDF mục 5, 6).

**`*ngIf` – điều kiện:**
```html
<div class="form-box" *ngIf="authService.hasRole('Admin')">   <!-- chỉ Admin thấy form -->
<tr *ngIf="danhSachSinhVien.length === 0">Chưa có sinh viên nào</tr>
<div *ngIf="errorMessage" class="error-banner">
```
- **Khác `[hidden]`** (PDF nhấn mạnh): `*ngIf` **xóa hẳn** khỏi DOM; `[hidden]` chỉ ẩn bằng CSS, phần tử vẫn còn. → Dùng `*ngIf` cho nút Admin là đúng: người khác mở F12 cũng không thấy nút.
- Có `else` với `<ng-template #elseBlock>` (PDF mục 3, 4).

**`ngSwitch`:** giống `switch-case` – dự án **không dùng** (không có chỗ nào cần nhiều nhánh hiển thị).

> Angular mới có cú pháp `@if`, `@for` thay cho `*ngIf`, `*ngFor`. Dự án vẫn dùng kiểu cũ – vẫn hợp lệ, đúng với PDF.

### 2.6 Attribute Directive – `ngClass`, `ngStyle` (PDF Phần 8, 9)

Đổi **class/style** của phần tử theo dữ liệu (không thêm/bớt phần tử).

| PDF | Dự án dùng dạng tương đương |
|---|---|
| `[ngClass]="{ odd: o, even: e }"` | Không dùng `ngClass` |
| `[ngStyle]="{'width': ...}"` | `[style.width.%]="tienTrinhUpload"` – thanh tiến trình upload rộng theo % |

`[style.width.%]` là **style binding** – cách ngắn hơn `ngStyle` khi chỉ đổi 1 thuộc tính.

### 2.7 Bootstrap (PDF Phần 4)

PDF hướng dẫn nhúng Bootstrap bằng CDN hoặc npm. **Dự án không dùng Bootstrap** – giao diện viết CSS riêng (`styles.css` + `.css` từng component, dùng biến màu `--color-sage`...). Nếu mentor hỏi: *"Em tự viết CSS để chủ động giao diện; Bootstrap là lựa chọn khác, có thể cài bằng `npm install bootstrap` rồi khai báo trong `angular.json → styles`."*

### 2.8 Những thứ PDF chưa nói nhưng dự án dùng (rất hay bị hỏi)

| Khái niệm | Bản chất | Trong dự án |
|---|---|---|
| **Service + Dependency Injection** | Lớp `@Injectable({providedIn:'root'})` chứa logic dùng chung; Angular tự tạo **1 bản duy nhất** và "tiêm" vào constructor component | `SinhVienService`, `AuthService`, `ToastService` |
| **HttpClient + Observable** | Gọi API trả về `Observable` – **chưa gửi** cho đến khi `.subscribe()` | `this.sinhVienService.getAll(...).subscribe({ next, error })` |
| **Router + Routes** | Bảng ánh xạ URL → Component | `app.routes.ts` |
| **Lifecycle Hook** | Các "mốc đời" của component | `ngOnInit` gọi API, `ngOnDestroy` hủy subscribe (Toast) |
| **Interceptor** | "Trạm kiểm soát" mọi request/response HTTP | `jwtInterceptor`, `errorInterceptor` |
| **Guard** | "Bảo vệ cửa" trước khi vào route | `authGuard`, `roleGuard` |
| **Zoneless + `detectChanges()`** | Dự án không dùng `zone.js` → Angular **không tự biết** dữ liệu đổi sau khi API trả về → phải gọi `this.cdr.detectChanges()` để vẽ lại | Có trong mọi `next:` của subscribe |
| **SSR (Server-Side Rendering)** | Trang được render trước trên Node.js; ở đó **không có `window`, `localStorage`** | Vì vậy code hay có `if (typeof window !== 'undefined')` và `isPlatformBrowser(...)` |

---

## 3. Tuần 1 – Nền tảng & bảo mật

### 3.1 Bắt buộc dùng DTO

| | |
|---|---|
| **Khái niệm** | **DTO (Data Transfer Object)** – lớp chỉ dùng để **truyền dữ liệu qua API**, khác với **Entity** (lớp ánh xạ bảng DB). |
| **Bản chất – vì sao** | Trả thẳng Entity ra ngoài sẽ: ① **lộ dữ liệu nhạy cảm** (`PasswordHash`, `IsDeleted`); ② client gửi lên có thể **ghi đè cột không được phép** (over-posting); ③ đổi DB là vỡ API. DTO là "tấm kính lọc" – chỉ cho đi những gì cần. |
| **Trong dự án** | Entity: `Models/SinhVien.cs` (có `IsDeleted`). DTO: `DTOs/SinhVienDto.cs` (không có `IsDeleted`, có thêm validation `[Required]`, `[EmailAddress]`, `[Range(18,99)]`). **Mapping** ở `SinhVienService.ToDto()` và `.Select(s => new SinhVienDto{...})`. Các DTO khác: `DangKyDto`, `DangNhapDto`, `AuditLogDto`, `PagedResult<T>`, `ErrorResponse`. |
| **Cách thấy trên web** | F12 → Network → request `SinhVien?...` → tab Response: không có `isDeleted`, không có `passwordHash`. |
| **Keyword** | DTO, Entity, Mapping, Over-posting, Data Annotation |

### 3.2 Quy tắc đặt tên (Naming Convention)

| Ngôn ngữ | Quy tắc | Ví dụ trong dự án |
|---|---|---|
| **C#** | Class, method, property: **PascalCase** | `SinhVienService`, `GetAllAsync`, `HoTen` |
| C# | Biến cục bộ, tham số: **camelCase**; field private: `_camelCase` | `sinhVienDto`, `_context` |
| **TypeScript** | Biến, hàm, property: **camelCase** | `danhSachSinhVien`, `taiDanhSach()`, `hoTen` |
| TypeScript | Class, interface: PascalCase | `SinhVienComponent`, `ApiError` |
| File Angular | **kebab-case** | `sinh-vien.ts`, `error-message.ts` |

**Bản chất:** C# và JS có quy ước khác nhau. ASP.NET Core **tự đổi** `HoTen` (C#) ↔ `hoTen` (JSON) khi gửi/nhận – nên hai bên đều đúng chuẩn mà vẫn khớp nhau.

**Keyword:** PascalCase, camelCase, kebab-case, JSON naming policy

### 3.3 Không Hardcode cấu hình

| | |
|---|---|
| **Khái niệm** | Chuỗi kết nối DB và khóa bí mật JWT **không viết cứng trong code**, mà đọc từ `appsettings.json`. |
| **Bản chất – vì sao** | Đổi máy / đổi môi trường (dev → server thật) chỉ sửa file cấu hình, **không biên dịch lại**. Code đưa lên git không chứa bí mật trực tiếp. |
| **Trong dự án** | `appsettings.json` có `ConnectionStrings:DefaultConnection` và `JwtSettings:Secret/Issuer/Audience`. Code đọc: `builder.Configuration.GetConnectionString("DefaultConnection")`, `builder.Configuration.GetSection("JwtSettings")`. |
| **Lưu ý thực tế** | File `appsettings.json` vẫn nằm trong git → đi làm thật thì Secret nên để ở **User Secrets** / biến môi trường. Với yêu cầu mentor thì đã đạt. |
| **Keyword** | appsettings.json, IConfiguration, Connection String, Hardcode, Secret |

### 3.4 Lưu mật khẩu dạng Hash

| | |
|---|---|
| **Khái niệm** | **Hash** = biến mật khẩu thành chuỗi **một chiều** – không thể giải ngược. |
| **Bản chất – vì sao** | Nếu DB bị lộ, kẻ tấn công không biết mật khẩu thật. **BCrypt** còn tự thêm **salt** (chuỗi ngẫu nhiên) → 2 người cùng mật khẩu `123` vẫn ra 2 hash khác nhau; và cố ý **chậm** để chống dò mật khẩu hàng loạt. |
| **Trong dự án** | Đăng ký: `BCrypt.Net.BCrypt.HashPassword(dto.Password)`. Đăng nhập: `BCrypt.Verify(matKhauNhap, hashTrongDb)` – băm lại rồi so, **không bao giờ giải mã**. |
| **Cách thấy** | SSMS: `SELECT Username, PasswordHash FROM Users` → dạng `$2a$11$...` (`2a` = phiên bản BCrypt, `11` = độ khó). Hôm nay đã kiểm: `demo_claude → $2a$11$3LlzJOuWGj4HE2GB...` |
| **Keyword** | Hash, BCrypt, Salt, Plain-text, One-way |

### 3.5 Gắn Token hợp lệ – JWT

| | |
|---|---|
| **Khái niệm** | **JWT (JSON Web Token)** = "thẻ ra vào" server cấp sau khi đăng nhập. Gồm 3 phần `header.payload.signature`. |
| **Bản chất – vì sao** | HTTP **không nhớ** ai vừa đăng nhập (stateless). Client phải gửi kèm "thẻ" ở **mỗi request**. Payload chứa `Name`, `Role`, `FullName`, hạn dùng; **chữ ký** tạo từ `Secret` → ai sửa payload (vd đổi Role thành Admin) thì chữ ký sai, server từ chối. |
| **Luồng trong dự án** | ① `XacThucController.DangNhap` kiểm mật khẩu → `GenerateJwtToken` tạo token (hạn **2 giờ**). ② Angular `AuthService.login` lưu token vào `localStorage`. ③ `jwtInterceptor` **tự gắn** `Authorization: Bearer <token>` vào mọi request. ④ API: `AddJwtBearer` kiểm Issuer, Audience, hạn, chữ ký → `UseAuthentication()` tạo `User` cho request. |
| **Chống giả mạo trên UI** | `AuthService.getRole()` đọc Role **từ token** chứ không tin `localStorage['role']`; nếu bị sửa tay thì trả lại Role thật và báo lỗi. |
| **Cách thấy** | F12 → Network → chọn 1 request `SinhVien` → **Request Headers** có dòng `Authorization: Bearer eyJhbGci...`. Dán token vào jwt.io để xem payload. |
| **Keyword** | JWT, Bearer Token, Claim, Signature, Stateless, Interceptor, Authentication |

### 3.6 Code bất đồng bộ (async/await)

| | |
|---|---|
| **Khái niệm** | Hàm `async` trả `Task`; `await` = "chờ kết quả mà **không giữ luồng**". |
| **Bản chất – vì sao** | Truy vấn DB mất thời gian chờ mạng/ổ đĩa. Nếu chặn đồng bộ, luồng xử lý (thread) ngồi không → server ít request đồng thời. Với `await`, luồng được **trả lại** để phục vụ người khác, xong việc mới quay lại. |
| **Trong dự án** | 100% hàm truy cập DB dùng `ToListAsync()`, `CountAsync()`, `FindAsync()`, `FirstOrDefaultAsync()`, `AnyAsync()`, `SaveChangesAsync()`. Commit "Doi sang AnyAsync de thuc hien bat dong bo" là ví dụ sửa lỗi này. |
| **Keyword** | async, await, Task, Thread, Non-blocking |

### 3.7 Load dữ liệu – `.Include()` và lỗi N+1

| | |
|---|---|
| **Khái niệm** | **N+1**: 1 câu lấy N dòng, rồi **mỗi dòng** lại chạy thêm 1 câu lấy dữ liệu liên quan → N+1 câu. **`.Include()`** (Eager Loading) gộp thành **1 câu JOIN**. |
| **Trong dự án** | Role là **cột chuỗi** trong bảng `Users`, không có bảng liên kết / khóa ngoại → **không phát sinh N+1, không cần Include**. Trả lời mentor đúng như vậy, kèm ví dụ: nếu có bảng `Lop` thì viết `_context.SinhVien.Include(s => s.Lop)`. |
| **Keyword** | Eager Loading, Lazy Loading, Include, N+1 Query, JOIN |

### 3.8 Vòng đời Component – gọi API trong `ngOnInit`

| | |
|---|---|
| **Khái niệm** | `constructor` chạy khi **tạo đối tượng** – chỉ dùng để nhận DI. `ngOnInit` chạy **sau khi Angular gắn xong dữ liệu đầu vào** – chỗ chuẩn để gọi API. |
| **Bản chất – vì sao** | Constructor nên nhẹ, không có tác dụng phụ (dễ test, không gọi API khi chỉ khởi tạo). `ngOnInit` đảm bảo component đã sẵn sàng. |
| **Trong dự án** | `SinhVienComponent.ngOnInit()` → `taiDanhSach()`; `LichSuComponent.ngOnInit()` → `taiLichSu()`. Constructor chỉ có `private sinhVienService: ...`. |
| **Keyword** | Lifecycle Hook, constructor, ngOnInit, ngOnDestroy |

### 3.9 Mô phỏng sập Server – không trắng trang

| | |
|---|---|
| **Yêu cầu** | Tắt API hoặc nhập sai mật khẩu → giao diện phải báo lỗi đàng hoàng. |
| **Cách hoạt động** | Server tắt → HttpClient nhận lỗi **status 0**. `errorInterceptor` bắt → `layThongBaoLoi()` trả "Không thể kết nối đến máy chủ..." → **Toast đỏ**. Trang danh sách còn hiện **banner + nút "Thử lại"**. Sai mật khẩu → API trả 400 JSON → trang Login hiện thông báo. |
| **Cách demo** | Đang ở trang sinh viên → tắt API → bấm Tìm kiếm → thấy toast + banner, không trắng trang. |
| **Keyword** | HttpErrorResponse, status 0, catchError, Toast |

### 3.10 Vấn đáp: CORS và AddAuthentication (Program.cs)

**CORS (Cross-Origin Resource Sharing):**
- Trình duyệt **chặn** trang ở `localhost:4200` gọi sang `localhost:7280` (khác "origin" = khác cổng) – đây là **Same-Origin Policy**.
- API phải "cấp phép": `policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()`.
- Trình duyệt gửi request **OPTIONS (preflight)** hỏi trước, API trả header `Access-Control-Allow-Origin` → mới gửi request thật.

**AddAuthentication + AddJwtBearer:**
- `DefaultAuthenticateScheme = JwtBearer` → mặc định đọc token từ header `Bearer`.
- `TokenValidationParameters` → 4 điều kiện kiểm: đúng **Issuer**, đúng **Audience**, **còn hạn**, **chữ ký** đúng `Secret`.

**Thứ tự Middleware (quan trọng – sai thứ tự là lỗi):**
```
ExceptionMiddleware → UseStatusCodePages → HttpsRedirection → Cors
→ StaticFiles → Authentication → Authorization → MapControllers
```
Authentication (**bạn là ai**) phải đứng trước Authorization (**bạn được làm gì**).

### 3.11 Lịch sử Git

Commit chia theo tính năng: `DTO` → `Them trang Login/Register` → `Chinh giao dien theo quyen` → `Upload anh` → `Audit logging` → `Quan Ly File Rac` → ... → `lam kiem thu nang cao`. Không dồn 1 commit "update cuối tuần".

**Keyword:** commit, atomic commit, commit message

---

## 4. Tuần 2 – Tính năng chính

### 4.1 Phân trang, tìm kiếm, sắp xếp phía Server

| | |
|---|---|
| **Khái niệm** | **Server-side pagination**: DB chỉ trả đúng **1 trang** dữ liệu, không trả toàn bộ. |
| **Bản chất – vì sao** | Bảng có 1 triệu dòng mà kéo hết lên RAM rồi mới cắt → chậm, tốn bộ nhớ, tốn mạng. **`IQueryable`** là "câu truy vấn chưa chạy": mỗi `.Where`, `.OrderBy`, `.Skip`, `.Take` chỉ **ghép thêm vào câu SQL**; tới `ToListAsync()` mới gửi **1 câu SQL** duy nhất xuống DB. |
| **Backend** | `SinhVienQuery` nhận `PageNumber, PageSize, Keyword, SortBy, IsDescending`. `SinhVienService.GetAllAsync`: `Where(keyword)` → `switch` chọn `OrderBy` → `CountAsync()` (tổng) → `Skip((page-1)*size).Take(size)` → `Select` sang DTO → `ToListAsync()`. Trả `PagedResult { Items, TotalCount, PageNumber, PageSize, TotalPages }`. Có chặn `PageSize` tối đa 50 để không ai xin 1 triệu dòng. |
| **SQL thật sinh ra** | `SELECT ... FROM SinhVien WHERE IsDeleted=0 AND (HoTen LIKE '%x%' OR ...) ORDER BY Tuoi DESC OFFSET 0 ROWS FETCH NEXT 3 ROWS ONLY` |
| **Frontend** | `sinh-vien.ts`: `chuyenTrang()`, `timKiem()` (về trang 1), `thayDoiSapXep(cot)` (bấm lần 2 đảo chiều) → đều gọi `taiDanhSach()` truyền tham số xuống API. |
| **Cách thấy** | Bấm "Trang sau", tiêu đề cột "Tuổi ↕", gõ từ khóa → F12 Network thấy URL `?pageNumber=2&pageSize=5&sortBy=Tuoi&isDescending=true`. Hôm nay test: trả 3 dòng, `totalCount: 8`, `totalPages: 3`. |
| **Keyword** | IQueryable, IEnumerable, Skip/Take, OFFSET FETCH, Deferred Execution, PagedResult |

> **IQueryable vs IEnumerable:** `IQueryable` lọc **dưới SQL Server**; `IEnumerable` (sau `ToList()`) lọc **trên RAM**. Gọi `.ToList()` quá sớm = lấy hết bảng.

### 4.2 Giao diện theo quyền & Route Guard

| | |
|---|---|
| **Khái niệm** | **Route Guard (`CanActivate`)**: hàm chạy **trước khi vào trang**, trả `true` (cho vào) hoặc `UrlTree` (chuyển hướng). **Role-based UI**: ẩn/hiện nút theo quyền bằng `*ngIf`. |
| **Trong dự án** | `app.routes.ts`: `/sinh-vien` có `authGuard` (phải đăng nhập); `/lich-su` có `roleGuard(['Admin'])`. `guards/role.ts`: chưa đăng nhập → `/login`; sai quyền → toast + về `/sinh-vien`. HTML: `*ngIf="authService.hasRole('Admin')"` cho form Thêm/Sửa, cột Thao tác, nút Lịch sử; nút "Đổi ảnh" cho Admin **hoặc** GiangVien. |
| **Bản chất – quan trọng** | Guard và `*ngIf` chỉ là **lớp UI cho đẹp** – người dùng có thể sửa JS. **Bảo mật thật** nằm ở backend: `[Authorize(Roles = "Admin")]` trên `Create/Update/Delete` và `AuditLogController`. Hôm nay test: GiangVien gọi POST trực tiếp → **403**. |
| **Cách thấy** | Đăng nhập GiangVien → không thấy form, không thấy nút Sửa/Xóa; gõ tay `/lich-su` → bị đẩy về và hiện toast "Không thể truy cập dưới quyền Admin." (đã chụp lại hôm nay). |
| **Keyword** | CanActivate, CanActivateFn, UrlTree, Route Guard, Role-based, `[Authorize(Roles)]`, 401 vs 403 |

> **401 Unauthorized** = chưa đăng nhập / token hỏng. **403 Forbidden** = đã đăng nhập nhưng **không đủ quyền**.

### 4.3 Audit Logging (lưu vết)

| | |
|---|---|
| **Khái niệm** | Ghi lại **Ai – Làm gì – Bảng nào – Lúc nào – Dữ liệu trước/sau** mỗi khi Thêm/Sửa/Xóa. |
| **Bản chất – vì sao** | Truy trách nhiệm, điều tra khi dữ liệu sai, yêu cầu kiểm toán. |
| **Trong dự án** | Bảng `AuditLogs (Username, Action, TableName, OldValues, NewValues, Timestamp)`. Tuần 2 làm bước đầu, **tuần 3-4 hoàn thiện bằng Interceptor** (xem 5.2). Màn hình `lich-su` gọi `GET /api/AuditLog` – chỉ Admin. |
| **Keyword** | Audit Log, Interceptor, Action Filter, Traceability |

### 4.4 Upload file

| | |
|---|---|
| **Khái niệm** | Gửi file bằng **`multipart/form-data`** (chia request thành nhiều phần, có phần nhị phân). Backend nhận bằng **`IFormFile`**. |
| **Backend** | `POST /api/SinhVien/upload-avatar/{id}` (Admin, GiangVien). Kiểm: có file, đuôi `.jpg/.jpeg/.png`, ≤ **2MB**. Lưu `wwwroot/avatars/<Guid>.jpg` (**Guid** để không trùng tên/ghi đè). Lưu đường dẫn `/avatars/...` vào cột `AvatarUrl`. |
| **Frontend** | `FormData.append('file', file)`; gọi `http.post(..., { reportProgress: true, observe: 'events' })` → nhận nhiều sự kiện: `UploadProgress` (tính `% = loaded/total`) và `Response` (xong). Thanh tiến trình dùng `[style.width.%]="tienTrinhUpload"`. Có **xem trước ảnh** bằng `FileReader.readAsDataURL`. |
| **Hiển thị ảnh** | `app.UseStaticFiles()` cho phép truy cập file trong `wwwroot` qua URL → `<img [src]="'https://localhost:7280' + sv.avatarUrl">`. |
| **Cách thấy** | Bấm "📷 Đổi ảnh" → chọn ảnh → thanh % chạy → ảnh mới hiện trong bảng. Thử file `.pdf` hoặc > 2MB → toast lỗi 400. |
| **Keyword** | IFormFile, multipart/form-data, FormData, reportProgress, HttpEventType, UseStaticFiles, wwwroot |

### 4.5 Chuẩn hóa xử lý lỗi + Toast

| | |
|---|---|
| **Khái niệm** | **Global Exception Middleware**: 1 chỗ duy nhất bắt mọi lỗi ở backend, trả về **cùng 1 khuôn JSON**. **HttpInterceptor** ở Angular bắt mọi lỗi HTTP, hiện **Toast** thay cho `alert()`. |
| **Bản chất – vì sao** | Không cần `try/catch` ở từng Controller; frontend chỉ cần 1 cách đọc lỗi; người dùng thấy thông báo thân thiện; 401 thì tự đẩy về Login. |
| **Chi tiết** | Xem mục 5.1 (tuần 3-4 dứt điểm). |
| **Keyword** | Middleware, Global Exception Handling, HttpInterceptor, Toast/Snackbar, 401 redirect |

---

## 5. Tuần 3-4 – Dứt điểm Exception, Audit, quản lý file rác

> Phần giao riêng cho **Nguyễn Thanh Phong**: ① Exception chuẩn `{ statusCode, message, details }` ② Audit bằng `SaveChangesInterceptor` ③ Xóa ảnh cũ khi cập nhật ảnh mới.

### 5.1 Exception Handling – format JSON chuẩn

**Khái niệm Middleware:** các "trạm" xếp nối nhau trong **pipeline**; mỗi request đi qua từng trạm theo thứ tự, response đi ngược lại. `ExceptionMiddleware` đứng **đầu tiên** nên bọc `try/catch` được **mọi thứ phía sau**.

**Luồng backend:**
```
Service gặp lỗi nghiệp vụ → throw new NotFoundException("Không tìm thấy...")
        ↓  (exception bay ngược lên qua Controller, Authorization...)
ExceptionMiddleware.catch(ex)
        ↓  switch theo KIỂU exception (pattern matching):
   AppException (BadRequest 400 / Forbidden 403 / NotFound 404 / Conflict 409) → dùng StatusCode của nó
   DbUpdateConcurrencyException → 409       DbUpdateException → 409
   ArgumentException → 400                  còn lại → 500 "Đã xảy ra sự cố hệ thống"
        ↓
Trả JSON: { "statusCode": 404, "message": "...", "details": "<stack trace – CHỈ ở Development>" }
```

**Các lỗi KHÔNG phải exception** (401/403 do `[Authorize]`, 404 sai URL) → `UseStatusCodePages` biến thành cùng khuôn JSON. Lỗi **validation DTO** (400) → `InvalidModelStateResponseFactory` trong Program.cs cũng trả `ErrorResponse` (kèm `errors` từng trường).

| File | Vai trò |
|---|---|
| `Exceptions/AppException.cs` | Lớp cha `AppException(message, statusCode)` + 4 lớp con |
| `DTOs/ErrorResponse.cs` | Khuôn JSON lỗi |
| `Middleware/ExceptionMiddleware.cs` | Bắt & dịch lỗi; lỗi 4xx log `Warning`, 5xx log `Error` |

**Luồng frontend:**
```
errorInterceptor (catchError)
  → layThongBaoLoi(err): status 0 → "Không thể kết nối..."; có body.message → dùng message
  → toastService.showError(...)        (trang Login/Register tự hiện banner nên không toast)
  → 401 → logout() + về /login         403 → về /sinh-vien
  → details (dev) → console.error để debug
```

**Vì sao `details` chỉ ở Development?** Stack trace lộ cấu trúc thư mục, tên lớp → thông tin quý cho hacker. Production để `null`.

**Cách thấy:** đã test hôm nay – 400 (sai mật khẩu), 401 (không token), 403 (sai quyền), 404 (Id 99999) **đều cùng 1 khuôn JSON**.

**Keyword:** Middleware Pipeline, RequestDelegate `_next`, Custom Exception, Pattern Matching, ErrorResponse, UseStatusCodePages, InvalidModelStateResponseFactory

### 5.2 Audit bằng `SaveChangesInterceptor`

| | |
|---|---|
| **Khái niệm** | **Interceptor** của EF Core = "móc" chen vào **ngay trước khi** EF ghi xuống DB. |
| **Bản chất – vì sao** | Cách cũ: mỗi Controller tự gọi hàm ghi log → **dễ quên**, lặp code. Interceptor tự động bắt **mọi** lần `SaveChangesAsync()` → không chỗ nào lọt. |
| **Cách hoạt động** | `AuditSaveChangesInterceptor.SavingChangesAsync()` → duyệt `ChangeTracker.Entries()` (EF đang theo dõi những đối tượng nào bị đổi) → với mỗi entry `Added / Modified / Deleted` tạo 1 `AuditEntry`: **Ai** (`IHttpContextAccessor` → `User.Identity.Name` từ JWT), **Hành động** (Thêm/Sửa/Xóa – và nếu `IsDeleted` thành `true` thì ghi là **Xóa** dù về mặt EF là Modified), **OldValues/NewValues** (chỉ các cột thực sự đổi, dạng JSON), bỏ qua cột nhạy cảm `PasswordHash`, `RowVersion`. Cuối cùng `Add` các `AuditLog` vào **cùng lần lưu** → cùng 1 transaction. |
| **Đăng ký** | Program.cs: `AddScoped<AuditSaveChangesInterceptor>()` và `options.AddInterceptors(auditInterceptor)` trong `AddDbContext`. |
| **Cách thấy** | Đăng nhập Admin → sửa 1 sinh viên → bấm "📜 Lịch sử hệ thống" → thấy dòng mới: admin / Sửa / SinhVien / Old `{"Tuoi":20}` New `{"Tuoi":21}`. Hôm nay DB có dòng `Anonymous – Thêm – Users` từ lúc tạo tài khoản demo (đăng ký chưa có token nên là Anonymous). |
| **Keyword** | SaveChangesInterceptor, ChangeTracker, EntityState, IHttpContextAccessor, OldValues/NewValues |

### 5.3 Quản lý file rác – xóa ảnh cũ

| | |
|---|---|
| **Vấn đề** | Mỗi lần đổi ảnh, file cũ vẫn nằm trên ổ → **rò rỉ dung lượng**. |
| **Cách làm (UploadAvatar)** | ① Ghi file mới. ② Nhớ `oldAvatarUrl`. ③ Cập nhật `AvatarUrl` mới → `SaveChangesAsync()`. ④ **Nếu lưu DB lỗi** → xóa file **mới** vừa ghi (không để rác), `throw` tiếp cho Middleware. ⑤ Lưu DB thành công mới **xóa file cũ**. |
| **Vì sao thứ tự này** | Xóa file cũ **sau** khi DB đã lưu → nếu DB lỗi, sinh viên vẫn còn ảnh cũ dùng được. An toàn dữ liệu trước, dọn rác sau. |
| **Cách thấy** | Mở thư mục `QuanLySinhVien/wwwroot/avatars`, đếm file → đổi ảnh 1 sinh viên → số file **không tăng**. |
| **Keyword** | Physical file, wwwroot, Storage leak, Rollback file |

---

## 6. Tuần 5-6 – Xóa mềm & Unit Test

### 6.1 Soft Delete + Global Query Filter

| | |
|---|---|
| **Khái niệm** | **Xóa mềm**: không `DELETE`, chỉ đặt cờ `IsDeleted = true`. **Global Query Filter**: điều kiện EF **tự gắn** vào **mọi** truy vấn của 1 bảng. |
| **Bản chất – vì sao** | Xóa cứng thì mất vĩnh viễn, mất lịch sử. Nhưng nếu chỉ có cờ thì phải nhớ thêm `Where(!IsDeleted)` ở **mọi** câu truy vấn → dễ quên. Query Filter khai báo **1 lần**. Ví von: **Thùng rác** – file vẫn còn trên ổ nhưng Explorer không hiện. |
| **Trong dự án** | Model: `public bool IsDeleted { get; set; } = false;`. DB: `ALTER TABLE SinhVien ADD IsDeleted BIT NOT NULL DEFAULT 0`. `AppDbContext.OnModelCreating`: `modelBuilder.Entity<SinhVien>().HasQueryFilter(s => !s.IsDeleted);`. Service: `DeleteAsync` → `sv.IsDeleted = true; SaveChangesAsync()` → SQL là **UPDATE**. |
| **Hệ quả hay** | Danh sách, đếm tổng, tìm theo Id, kiểm trùng Email… **tự động** bỏ qua sinh viên đã xóa. Muốn xem cả đã xóa: `.IgnoreQueryFilters()`. |
| **Phạm vi** | Chỉ áp dụng cho **Sinh viên**, không áp dụng cho tài khoản. |
| **Cách thấy** | Admin xóa 1 sinh viên → biến mất khỏi web → SSMS: `SELECT Id, HoTen, IsDeleted FROM SinhVien` → dòng **vẫn còn**, `IsDeleted = 1`. Hôm nay DB có 19 dòng, 11 đã xóa mềm, web chỉ hiện 8. |
| **Keyword** | Soft Delete, Hard Delete, HasQueryFilter, IgnoreQueryFilters, Flag column |

### 6.2 Tách tầng Service + Unit Test

| | |
|---|---|
| **Khái niệm** | **Tầng Service** chứa nghiệp vụ; **Controller** chỉ nhận request và trả response ("mỏng"). **Unit Test** = code tự động kiểm tra **1 đơn vị nhỏ** (1 hàm) có đúng không. |
| **Bản chất – vì sao tách** | Controller dính HTTP → khó test. Service là lớp C# thuần → gọi trực tiếp trong test được. Mỗi lớp **1 trách nhiệm** (Single Responsibility). |
| **Trong dự án** | `Services/SinhVienService.cs`: `GetAllAsync`, `GetByIdAsync`, `CreateAsync` (chặn trùng email → 409), `UpdateAsync` (Id khớp, RowVersion, trùng email), `DeleteAsync` (xóa mềm), helper `TimHoacBaoLoiAsync` (không có → 404). Controller chỉ còn 1 dòng mỗi action: `=> Ok(await _service.GetAllAsync(query))`. Đăng ký DI: `AddScoped<SinhVienService>()`. |
| **Test** | `QuanLySinhVien.Tests/SinhVienServiceTests.cs` (xUnit, DB **InMemory**) và `ExceptionMiddlewareTests.cs` (Moq giả `ILogger`, `IHostEnvironment`). Khuôn **AAA**: **Arrange** (chuẩn bị dữ liệu) – **Act** (gọi hàm) – **Assert** (kiểm kết quả, vd `await Assert.ThrowsAsync<ConflictException>(...)`). |
| **Cách chạy** | `dotnet test` (không phải trên web). |
| **Keyword** | Service Layer, Thin Controller, Dependency Injection, AddScoped, xUnit, `[Fact]`, Moq, InMemory Database, AAA pattern |

> **AddScoped** = mỗi **request** HTTP có 1 bản `SinhVienService` và 1 `AppDbContext` riêng → không lẫn dữ liệu giữa người dùng.

---

## 7. Tuần 7 – Integration Test, Coverage, Concurrency

> Nên hiểu theo thứ tự **7.3 → 7.1 → 7.2**: Concurrency là tính năng mới; Integration Test chứng minh nó chạy trên SQL thật; Coverage đo chất lượng test.

### 7.3 Optimistic Concurrency với RowVersion

| | |
|---|---|
| **Vấn đề** | A và B cùng mở form sửa sinh viên 5. A lưu tuổi 21. B (vẫn thấy dữ liệu cũ) lưu email mới → **ghi đè mất** thay đổi của A mà không ai biết ("lost update"). |
| **Khái niệm** | **Optimistic Concurrency** (lạc quan): không khóa dòng khi đọc; **lúc lưu mới kiểm** xem có ai sửa trước chưa. **RowVersion** = cột SQL Server **tự tăng** mỗi khi dòng bị sửa – như "số phiên bản". |
| **Cách hoạt động** | Model: `[Timestamp] public byte[]? RowVersion`. DB: `ALTER TABLE SinhVien ADD RowVersion ROWVERSION`. ① GET trả kèm `rowVersion` (vd `"AAAAAAABX5M="`). ② Angular giữ lại trong form (`sua(sv)` copy `rowVersion`). ③ PUT gửi lại `rowVersion` cũ. ④ Service: `Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;` → EF sinh `UPDATE ... WHERE Id=5 AND RowVersion=<bản cũ>`. ⑤ Nếu người khác đã sửa → RowVersion đã đổi → **0 dòng bị cập nhật** → EF ném `DbUpdateConcurrencyException` → Service đổi thành `ConflictException` → **409** "Dữ liệu đã bị người khác thay đổi...". ⑥ Angular thấy 409 này → reset form, tải lại danh sách. |
| **Vì sao dòng ★ quan trọng** | Service đọc `sv` từ DB **mới nhất**; nếu không ghi đè `OriginalValue` bằng bản client gửi lên, EF sẽ so với bản **vừa đọc** → luôn khớp → không bao giờ phát hiện xung đột. |
| **Cách thấy** | Mở 2 tab cùng Admin, cùng bấm Sửa sinh viên X. Tab 1 lưu. Tab 2 lưu → toast đỏ 409, form tự làm mới. |
| **Keyword** | Optimistic vs Pessimistic Concurrency, RowVersion, `[Timestamp]`, Concurrency Token, OriginalValue, DbUpdateConcurrencyException, 409 Conflict, Lost Update |

### 7.1 Integration Test với TestContainers

| | |
|---|---|
| **Khái niệm** | **Integration Test** kiểm **nhiều tầng ghép lại**: HTTP → Middleware → JWT → Controller → Service → EF → **SQL Server thật**. |
| **Bản chất – vì sao** | Unit Test (InMemory) không kiểm được routing, `[Authorize]`, Middleware, và InMemory **không sinh RowVersion**, không kiểm ràng buộc như SQL thật. Nhưng test vào DB `QLSINHVIEN` thật thì làm bẩn dữ liệu. |
| **Công cụ** | **`WebApplicationFactory<Program>`**: chạy cả API **trong bộ nhớ**, cho `HttpClient` gọi vào như Angular. **TestContainers (`MsSqlContainer`)**: tự bật 1 SQL Server **trong Docker** khi test bắt đầu, xóa khi xong. |
| **Trong dự án** | `IntegrationTests/ApiFactory.cs`: đổi connection string sang container; **chốt an toàn** – nếu chuỗi chứa `QLSINHVIEN` thì dừng ngay; `EnsureCreatedAsync()` tạo bảng. `public partial class Program { }` cuối Program.cs để test "nhìn thấy" lớp Program. `SinhVienApiTest.cs` có 6 kịch bản: Tạo rồi đọc lại; Email trùng → 409; Xóa là xóa mềm; 401 và 403; Tuổi sai → 400; **Hai người cùng sửa → người sau 409**. |
| **Kết quả hôm nay** | Docker 29.8.1 đang chạy → toàn bộ test pass. |
| **Keyword** | Integration Test, WebApplicationFactory, TestContainers, Docker, MsSqlContainer, IAsyncLifetime, IClassFixture |

### 7.2 Code Coverage ≥ 80%

| | |
|---|---|
| **Khái niệm** | **Coverage** = % số dòng code **được chạy qua** khi chạy test. |
| **Bản chất** | Đo xem test đã "đi" tới những nhánh nào. Coverage cao **không chứng minh** code đúng (test có thể không `Assert`), nhưng coverage thấp chắc chắn có chỗ **chưa ai kiểm**. |
| **Cách đo** | `dotnet test --collect:"XPlat Code Coverage"` (coverlet) → file `coverage.cobertura.xml` → `reportgenerator` → HTML trong `CoverageReport/`. |
| **Kết quả hôm nay** | `SinhVienService`: **100%** dòng (mọi hàm `GetAll / GetById / Create / Update / Delete / TimHoacBaoLoi` = line-rate 1). Yêu cầu ≥ 80% → **đạt**. |
| **Keyword** | Code Coverage, Line coverage, Branch coverage, coverlet, Cobertura, ReportGenerator |

---

## 8. Bảng keyword tổng hợp

### Angular

| Keyword | Giải thích 1 dòng |
|---|---|
| SPA | Chỉ 1 trang HTML, JS tự vẽ lại nội dung |
| Angular CLI / `ng serve` | Công cụ tạo & chạy dự án, cổng 4200 |
| Entry point (`main.ts`) | File đầu tiên chạy, khởi động ứng dụng |
| Standalone Component | Component tự khai báo `imports`, không cần NgModule |
| `@Component` / selector | Đánh dấu component / tên thẻ để nhúng |
| Interpolation `{{ }}` | Hiện giá trị từ class ra view |
| Property binding `[ ]` | Gán thuộc tính HTML từ class |
| Event binding `( )` | Gọi hàm khi có sự kiện |
| Two-way `[(ngModel)]` | Đồng bộ 2 chiều, dùng cho form |
| `*ngFor` / `*ngIf` | Lặp / điều kiện – thêm bớt DOM |
| `[hidden]` | Chỉ ẩn bằng CSS, vẫn còn trong DOM |
| `ngClass` / `ngStyle` / `[style.x]` | Đổi class/style theo dữ liệu |
| Service / `@Injectable` | Lớp logic dùng chung, được tiêm vào component |
| Dependency Injection | Angular tự tạo & đưa đối tượng vào constructor |
| HttpClient / Observable / subscribe | Gọi API; chỉ gửi khi subscribe |
| Lifecycle `ngOnInit` | Chỗ chuẩn để gọi API khi mở trang |
| HttpInterceptor | Chặn mọi request/response (gắn token, bắt lỗi) |
| Route Guard `CanActivate` | Kiểm tra trước khi vào trang |
| `localStorage` | Lưu token trong trình duyệt |
| Zoneless / `detectChanges()` | Không có zone.js → tự báo Angular vẽ lại |
| SSR / `isPlatformBrowser` | Render trên server, không có `window` |

### Backend / Database

| Keyword | Giải thích 1 dòng |
|---|---|
| DTO vs Entity | Dữ liệu truyền qua API vs ánh xạ bảng DB |
| appsettings.json / IConfiguration | Nơi để cấu hình, không hardcode |
| BCrypt / Hash / Salt | Băm mật khẩu một chiều, có muối ngẫu nhiên |
| JWT / Bearer / Claim | Thẻ đăng nhập có chữ ký; chứa thông tin người dùng |
| Authentication vs Authorization | Bạn là ai vs Bạn được làm gì |
| `[Authorize(Roles="Admin")]` | Chặn API theo quyền |
| 400 / 401 / 403 / 404 / 409 / 500 | Sai dữ liệu / chưa đăng nhập / không đủ quyền / không có / xung đột / lỗi server |
| CORS / Preflight | Cho phép origin khác gọi API / request OPTIONS hỏi trước |
| Middleware Pipeline | Chuỗi trạm xử lý request theo thứ tự |
| async / await / Task | Không chặn luồng khi chờ DB |
| IQueryable / Deferred execution | Truy vấn chưa chạy, ghép thành 1 câu SQL |
| Skip / Take | Phân trang (OFFSET / FETCH) |
| Include / N+1 | Eager loading / lỗi truy vấn lặp |
| EF Core / DbContext / DbSet | ORM – thao tác DB bằng C# |
| ChangeTracker / EntityState | EF theo dõi đối tượng Added/Modified/Deleted |
| SaveChangesInterceptor | Móc chạy trước khi lưu DB – dùng cho Audit |
| IFormFile / multipart | Nhận file upload |
| UseStaticFiles / wwwroot | Cho truy cập file tĩnh qua URL |
| Soft Delete / HasQueryFilter | Xóa bằng cờ / tự lọc mọi truy vấn |
| Service Layer / AddScoped | Tách nghiệp vụ / 1 bản cho mỗi request |
| Unit Test / xUnit / Moq / AAA | Test 1 hàm / framework / đối tượng giả / khuôn viết test |
| Integration Test / TestContainers | Test xuyên tầng / SQL thật trong Docker |
| Code Coverage | % dòng code được test chạy qua |
| Optimistic Concurrency / RowVersion | Kiểm xung đột lúc lưu / số phiên bản dòng |

---

## 9. Cách tự chạy lại

**Bước 1 – Backend** (cần SQL Server `MSI\SQLEXPRESS` đang chạy):
```bash
cd D:/TT/QuanLySinhVien/QuanLySinhVien
dotnet run --launch-profile https
```
→ API ở `https://localhost:7280`.

**Bước 2 – Frontend:**
```bash
cd D:/TT/QuanLySinhVienAngular
npm start
```
→ mở `http://localhost:4200`.

**Bước 3 – Test** (tắt API trước, vì API đang chạy sẽ khóa file `.dll`; cần Docker Desktop bật cho Integration Test):
```bash
cd D:/TT/QuanLySinhVien
dotnet test --collect:"XPlat Code Coverage"
```

**Kịch bản demo nhanh cho mentor (5 phút):**
1. Đăng nhập Admin → F12 Network → chỉ header `Authorization: Bearer ...` (T1).
2. SSMS: `SELECT * FROM Users` → cột hash (T1).
3. Chuyển trang, tìm kiếm, bấm sắp xếp cột → chỉ URL tham số (T2).
4. Đổi ảnh → thanh % → mở `wwwroot/avatars` thấy file cũ đã bị xóa (T2, T3-4).
5. Sửa / Xóa 1 sinh viên → vào "Lịch sử hệ thống" (T2, T3-4) → SSMS thấy `IsDeleted = 1` (T5-6).
6. Đăng xuất, đăng nhập GiangVien → không có nút; gõ `/lich-su` bị chặn (T2).
7. Tắt API → bấm Tìm kiếm → toast + banner, không trắng trang (T1, T2).
8. 2 tab cùng sửa 1 sinh viên → tab sau bị 409 (T7).
9. `dotnet test` → 32/32 pass, mở `CoverageReport/index.html` (T5-7).
