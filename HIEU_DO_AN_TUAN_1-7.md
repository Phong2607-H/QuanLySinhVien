# HIỂU ĐỒ ÁN QUẢN LÝ SINH VIÊN
### Khái niệm · Bản chất · Cách hoạt động · Cách chạy trên web · Keyword (Tuần 1 → Tuần 7)

> Người thực hiện: Nguyễn Thanh Phong
> Viết dựa trên code thật trong `D:\TT\QuanLySinhVien` (backend) và `D:\TT\QuanLySinhVienAngular` (frontend), kết hợp kiến thức Angular chuẩn.

---

## MỤC LỤC

- **Phần 1 – Đồ án đang làm gì?** (bức tranh tổng thể, cách khởi chạy)
- **Phần 2 – Kiến thức Angular dùng trong đồ án** (12 khái niệm nền)
- **Phần 3 – Kiến thức Backend dùng trong đồ án** (6 khái niệm nền)
- **Phần 4 – Từng yêu cầu Tuần 1 → Tuần 7** (19 yêu cầu)
  - Tuần 1: Nền tảng (6 yêu cầu)
  - Tuần 2: Tính năng chính (5 yêu cầu)
  - Tuần 3-4: Chuyên nghiệp hóa (3 yêu cầu)
  - Tuần 5-6: Bảo vệ dữ liệu và kiểm chứng code (2 yêu cầu)
  - Tuần 7: Chứng minh trong điều kiện thật (3 yêu cầu)
- **Phần 5 – Bảng tra cứu nhanh** (mã HTTP, tổng hợp 19 yêu cầu)

**Cách đọc mỗi yêu cầu ở Phần 4:**

| Mục | Trả lời câu hỏi |
|---|---|
| 📘 **Khái niệm** | Nó là gì? |
| 💡 **Bản chất** | Nó giải quyết vấn đề gì? Vì sao phải làm? |
| ⚙️ **Cách hoạt động** | Bên trong chạy thế nào? Trong đồ án làm ở đâu? |
| 🌐 **Chạy trên web** | Thao tác gì trên trình duyệt để thấy nó hoạt động? |
| 🔑 **Keyword** | Từ khóa quan trọng + giải thích |

---

# PHẦN 1 – ĐỒ ÁN ĐANG LÀM GÌ?

## 1.1. Mục đích

Một **ứng dụng web quản lý sinh viên**:
- **Đăng ký / đăng nhập** tài khoản.
- **Admin**: xem, thêm, sửa, xóa sinh viên, đổi ảnh đại diện, xem lịch sử thao tác.
- **Giảng viên**: chỉ xem danh sách và đổi ảnh đại diện.
- Danh sách có **tìm kiếm, sắp xếp, phân trang**.

## 1.2. Ba thành phần

```
┌──────────────────────┐   HTTP + JSON    ┌──────────────────────┐   SQL   ┌──────────────┐
│  FRONTEND (Angular)  │ ───────────────► │  BACKEND (ASP.NET)   │ ──────► │  SQL Server  │
│  http://localhost:4200│ ◄─────────────── │  https://localhost:7280│ ◄────── │  QLSINHVIEN  │
│  Giao diện người dùng │                  │  Web API, nghiệp vụ  │         │  Lưu dữ liệu │
└──────────────────────┘                  └──────────────────────┘         └──────────────┘
```

| Thành phần | Vai trò | Công nghệ |
|---|---|---|
| **Frontend** | Hiển thị giao diện, nhận thao tác, gọi API | Angular 22 (standalone, zoneless, SSR), TypeScript, RxJS |
| **Backend** | Nhận request, kiểm tra quyền, xử lý nghiệp vụ, đọc/ghi DB, trả JSON | ASP.NET Core Web API .NET 10, EF Core, JWT, BCrypt |
| **Database** | Lưu sinh viên, tài khoản, lịch sử | SQL Server Express, bảng `SinhVien`, `Users`, `AuditLogs` |
| **Test** | Kiểm chứng tự động | xUnit, Moq, EF InMemory, TestContainers (Docker), Coverlet |

**Vì sao tách frontend và backend?** Mỗi phần làm một việc: Angular lo hiển thị, API lo dữ liệu và bảo mật. API có thể phục vụ nhiều loại client (web, mobile) mà không phải viết lại.

## 1.3. Cách khởi chạy trên máy

| Bước | Làm gì | Kết quả |
|---|---|---|
| 1 | Bật **SQL Server** (`MSI\SQLEXPRESS`), có database `QLSINHVIEN` | Có nơi lưu dữ liệu |
| 2 | Mở `QuanLySinhVien.slnx` bằng Visual Studio → **Ctrl+F5** | API chạy ở `https://localhost:7280` |
| 3 | Mở CMD tại `D:\TT\QuanLySinhVienAngular` → `npm install` (lần đầu) → `ng serve` | Web chạy ở `http://localhost:4200` |
| 4 | Mở trình duyệt vào `http://localhost:4200` | Thấy trang đăng nhập |
| 5 | (Khi chạy test) bật **Docker Desktop** → Test Explorer → Run All | Toàn bộ test chạy |

> **Ctrl+F5 thay vì F5:** F5 chạy kèm debugger, Visual Studio sẽ dừng lại mỗi khi có exception (kể cả exception cố ý như 409), gây hiểu nhầm là lỗi.

---

# PHẦN 2 – KIẾN THỨC ANGULAR DÙNG TRONG ĐỒ ÁN

## 2.1. SPA – Single Page Application
- **Khái niệm:** ứng dụng web chỉ tải **một trang HTML** duy nhất; khi chuyển màn hình, Angular **thay nội dung** bằng JavaScript thay vì tải trang mới từ server.
- **Bản chất:** web mượt như ứng dụng máy tính; server chỉ trao đổi **dữ liệu JSON** chứ không gửi cả trang HTML.
- **Trong đồ án:** `/login`, `/sinh-vien`, `/lich-su` đều nằm trong một trang; `<router-outlet>` là chỗ Angular "thay ruột".

## 2.2. TypeScript
- **Khái niệm:** JavaScript **có kiểu dữ liệu**.
- **Bản chất:** phát hiện lỗi khi viết code (gõ sai tên trường, sai kiểu) thay vì khi chạy.
- **Trong đồ án:** `interface SinhVien { id: number; hoTen: string; ... rowVersion?: string }` – dấu `?` = có thể không có.

## 2.3. Component (Standalone)
- **Khái niệm:** một **mảnh giao diện** gồm 3 phần: **TypeScript** (dữ liệu + hàm), **HTML** (template), **CSS**.
- **Bản chất:** chia giao diện thành khối nhỏ, mỗi khối tự quản lý dữ liệu của mình, dùng lại được.
- **Standalone:** component **tự khai báo** những gì nó cần trong `imports: [...]`, không cần `NgModule` (cách cũ).
- **Trong đồ án:** `LoginComponent`, `RegisterComponent`, `SinhVienComponent`, `LichSuComponent`, `ToastComponent`; `App` là component gốc chứa `<app-toast>` và `<router-outlet>`.

```ts
@Component({
  selector: 'app-sinh-vien',               // tên thẻ HTML
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],  // *ngIf, ngModel, routerLink
  templateUrl: './sinh-vien.html',
  styleUrl: './sinh-vien.css'
})
export class SinhVienComponent implements OnInit { ... }
```

## 2.4. Template và Data Binding
- **Khái niệm:** cách **nối dữ liệu** giữa TypeScript và HTML.
- **Bản chất:** dữ liệu đổi → giao diện đổi theo; người dùng thao tác → gọi hàm trong TypeScript. Không phải tự viết code tìm thẻ HTML để sửa.

| Cú pháp | Tên | Chiều | Ví dụ trong đồ án |
|---|---|---|---|
| `{{ ... }}` | Interpolation | TS → HTML | `{{ getUserName() }}`, `{{ tienTrinhUpload }}%` |
| `[thuocTinh]="..."` | Property binding | TS → HTML | `[src]="previewAvatarUrl"`, `[disabled]="trangHienTai === 1"`, `[style.width.%]="tienTrinhUpload"` |
| `(suKien)="..."` | Event binding | HTML → TS | `(click)="timKiem()"`, `(change)="chonFileChoForm($event)"` |
| `[(ngModel)]="..."` | Two-way binding | Hai chiều | `[(ngModel)]="sinhVien.hoTen"`, `[(ngModel)]="tuKhoaTimKiem"` |

## 2.5. Directive (`*ngIf`, `*ngFor`)
- **Khái niệm:** "chỉ thị" thay đổi cấu trúc / hành vi của HTML.
- **Bản chất:** hiển thị có điều kiện và lặp danh sách mà không viết JavaScript thao tác DOM.

| Directive | Làm gì | Ví dụ |
|---|---|---|
| `*ngIf="dieuKien"` | Điều kiện sai → phần tử **không được tạo** | `*ngIf="authService.hasRole('Admin')"` ẩn form với Giảng viên; `*ngIf="errorMessage"` |
| `*ngFor="let sv of danhSachSinhVien"` | Lặp tạo nhiều phần tử | Mỗi sinh viên một dòng `<tr>` |

## 2.6. Service và Dependency Injection (DI)
- **Khái niệm:** **Service** là lớp chứa logic **dùng chung** (gọi API, lưu đăng nhập, phát thông báo). **DI** là cơ chế Angular **tự tạo và đưa** service vào nơi cần.
- **Bản chất:** component chỉ lo hiển thị; logic dùng chung viết một lần. `providedIn: 'root'` = cả ứng dụng dùng **chung một bản** (singleton).
- **Trong đồ án:** `AuthService`, `SinhVienService`, `AuditLogService`, `ToastService`.

```ts
@Injectable({ providedIn: 'root' })
export class SinhVienService {
  constructor(private http: HttpClient) { }   // Angular tự đưa HttpClient vào
}
// Trong hàm (guard, interceptor) không có constructor → dùng inject()
const authService = inject(AuthService);
```

## 2.7. HttpClient, Observable và RxJS
- **Khái niệm:**
  - **HttpClient**: công cụ Angular để gọi API (`get`, `post`, `put`, `delete`).
  - **Observable**: "dòng dữ liệu sẽ đến trong tương lai". Gọi API trả về Observable, **chưa gửi request** cho đến khi `subscribe`.
  - **RxJS**: thư viện xử lý Observable bằng các toán tử (`pipe`, `tap`, `catchError`).
- **Bản chất:** gọi API là việc **bất đồng bộ** – kết quả đến sau. Observable cho cách viết gọn: "khi có kết quả thì làm A, khi lỗi thì làm B".

```ts
this.sinhVienService.getAll(...).subscribe({
  next: (res) => { this.danhSachSinhVien = res.items; },   // thành công
  error: (err) => { ... }                                   // thất bại
});
```

| Toán tử | Ý nghĩa | Dùng ở |
|---|---|---|
| `subscribe({ next, error })` | Bắt đầu chạy, nhận kết quả hoặc lỗi | Mọi component |
| `pipe(...)` | Gắn thêm các bước xử lý | `AuthService.login`, `errorInterceptor` |
| `tap(...)` | Làm việc phụ, **không đổi** dữ liệu | Lưu token khi đăng nhập |
| `catchError(...)` | Bắt lỗi | `errorInterceptor` |
| `throwError(...)` | Ném tiếp lỗi | `errorInterceptor` |
| `Subject` | Một nơi phát – nhiều nơi nghe | `ToastService` |

## 2.8. Routing và Guard
- **Khái niệm:** **Routing** ánh xạ **URL → Component**. **Guard** là hàm chạy **trước** khi vào một route để quyết định cho vào hay không.
- **Bản chất:** SPA vẫn có URL riêng cho từng màn hình (bookmark, F5 được); guard chặn người chưa đăng nhập / không đủ quyền.

```ts
export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'sinh-vien', component: SinhVienComponent, canActivate: [authGuard] },
  { path: 'lich-su', component: LichSuComponent, canActivate: [roleGuard(['Admin'])] },
  { path: '', redirectTo: '/sinh-vien', pathMatch: 'full' },
  { path: '**', redirectTo: '/sinh-vien' }        // URL lạ → về trang chính
];
```

| Thành phần | Ý nghĩa |
|---|---|
| `<router-outlet>` | Chỗ hiển thị component của route hiện tại |
| `routerLink="/lich-su"` | Chuyển trang không tải lại |
| `router.navigate([...])` | Chuyển trang bằng code |
| `CanActivateFn` | Kiểu hàm guard; trả `true` = cho vào |
| `UrlTree` (`router.parseUrl('/login')`) | Guard trả về để **chuyển hướng** sang trang khác |

## 2.9. HTTP Interceptor
- **Khái niệm:** "trạm kiểm soát" mà **mọi** request/response của HttpClient đều đi qua.
- **Bản chất:** việc chung cho mọi request (gắn token, bắt lỗi) viết **một lần** thay vì lặp lại ở từng service.
- **Trong đồ án:** `jwtInterceptor` (gắn token), `errorInterceptor` (bắt lỗi). Đăng ký: `provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor]))`.

## 2.10. Change Detection và Zoneless
- **Khái niệm:** **Change Detection** là việc Angular kiểm tra dữ liệu đã đổi chưa để **vẽ lại** giao diện. Trước đây thư viện **Zone.js** tự theo dõi mọi sự kiện bất đồng bộ để kích hoạt việc này. **Zoneless** = không dùng Zone.js (mặc định ở Angular mới; dự án không cài `zone.js`).
- **Bản chất:** nhẹ và nhanh hơn, nhưng khi gán biến trong callback HTTP hay `setTimeout`, Angular **không tự biết** → phải báo bằng `ChangeDetectorRef.detectChanges()`.
- **Trong đồ án:** login, lịch sử, danh sách, toast, upload đều gọi `this.cdr.detectChanges()` sau khi đổi dữ liệu.

## 2.11. SSR / Prerender và `isPlatformBrowser`
- **Khái niệm:** **SSR (Server-Side Rendering)** – Angular có thể chạy **trên server (Node.js)** để tạo sẵn HTML. Dự án cấu hình `RenderMode.Prerender` cho mọi route (tạo HTML sẵn lúc build).
- **Bản chất:** trang hiện nhanh hơn, tốt cho SEO. Nhưng trên server **không có** `window`, `localStorage`, `sessionStorage`.
- **Trong đồ án:** mọi chỗ đụng tới `localStorage` đều kiểm tra trước: `isPlatformBrowser(PLATFORM_ID)` hoặc `typeof window !== 'undefined'` (trong `AuthService`, `jwtInterceptor`, `errorInterceptor`, guard, `LichSuComponent.ngOnInit`).

## 2.12. Lifecycle Hook, localStorage và sessionStorage
| Thứ | Ý nghĩa | Trong đồ án |
|---|---|---|
| `ngOnInit()` | Chạy **một lần** khi component được tạo → nơi tải dữ liệu ban đầu | Tải danh sách, tải lịch sử, đăng ký nghe Toast |
| `ngOnDestroy()` | Chạy khi component bị hủy → dọn dẹp | `ToastComponent` hủy `subscribe` để không rò bộ nhớ |
| `localStorage` | Lưu trong trình duyệt, **còn sau khi đóng tab** | `token`, `fullName`, `role` |
| `sessionStorage` | Lưu trong trình duyệt, **mất khi đóng tab** | Toast tạm `flashToast` khi đang chuyển trang |

---

# PHẦN 3 – KIẾN THỨC BACKEND DÙNG TRONG ĐỒ ÁN

## 3.1. Web API, Controller và HTTP
- **Khái niệm:** **Web API** là chương trình nhận **HTTP request**, trả **JSON**. **Controller** là lớp chứa các hàm xử lý (action).
- **Bản chất:** mỗi URL + phương thức HTTP ứng với một hành động.

| Phương thức | Ý nghĩa | Ví dụ trong đồ án | Mã thành công |
|---|---|---|---|
| `GET` | Đọc | `GET /api/SinhVien?pageNumber=1` | 200 OK |
| `POST` | Tạo mới | `POST /api/SinhVien` | 201 Created |
| `PUT` | Cập nhật | `PUT /api/SinhVien/5` | 204 No Content |
| `DELETE` | Xóa | `DELETE /api/SinhVien/5` | 204 No Content |

`[Route("api/[controller]")]` → `[controller]` tự thay bằng tên lớp bỏ chữ "Controller" (`SinhVienController` → `/api/SinhVien`).

## 3.2. Entity Framework Core (EF Core) và DbContext
- **Khái niệm:** **ORM** – công cụ cho phép làm việc với DB bằng **lớp C#** và **LINQ** thay vì viết SQL tay. **DbContext** (`AppDbContext`) là "cổng" kết nối DB; mỗi `DbSet<T>` là một bảng.
- **Bản chất:** viết `_context.SinhVien.Where(...)`, EF tự dịch thành SQL; `SaveChangesAsync()` tự sinh INSERT/UPDATE/DELETE.
- **ChangeTracker:** EF ghi nhớ đối tượng nào đã **thêm/sửa/xóa** để biết cần sinh câu SQL nào (dùng lại ở Audit Tuần 3-4 và RowVersion Tuần 7).

## 3.3. Middleware Pipeline
- **Khái niệm:** chuỗi các "trạm" mà **mọi request** đi qua **theo thứ tự** khai báo `app.UseXxx()` trong `Program.cs`.
- **Bản chất:** các việc chung (bắt lỗi, xác thực, CORS) làm một lần cho mọi request.
- **Thứ tự trong đồ án:** `ExceptionMiddleware` → `UseStatusCodePages` → `UseHttpsRedirection` → `UseCors` → `UseStaticFiles` → `UseAuthentication` → `UseAuthorization` → `MapControllers`.

## 3.4. Dependency Injection và vòng đời
- **Khái niệm:** lớp **xin** thứ nó cần qua constructor, ASP.NET **tự tạo và đưa vào**.
- **Bản chất:** không tự `new` → dễ thay thế (test thì đưa DB giả), không quản lý vòng đời bằng tay.

| Vòng đời | Sống bao lâu | Trong đồ án |
|---|---|---|
| `AddScoped` | Một request | `AppDbContext`, `SinhVienService`, `AuditSaveChangesInterceptor` |
| `AddSingleton` | Suốt chương trình | – |
| `AddTransient` | Mỗi lần xin là tạo mới | – |

## 3.5. CORS
- **Khái niệm:** *Cross-Origin Resource Sharing* – trình duyệt **chặn** trang ở địa chỉ này gọi API ở địa chỉ khác, trừ khi API cho phép.
- **Bản chất:** Angular ở `localhost:4200`, API ở `localhost:7280` → khác "origin" → phải cấu hình.
- **Trong đồ án:** `AddCors` policy `"AllowAngular"` cho `http://localhost:4200` + `app.UseCors("AllowAngular")`. Thiếu nó → mọi request báo lỗi, Angular nhận `status 0`.

## 3.6. Mã trạng thái HTTP
| Mã | Ý nghĩa | Khi nào trong đồ án |
|---|---|---|
| **200** OK | Thành công, có dữ liệu | Lấy danh sách, đăng nhập |
| **201** Created | Tạo mới thành công | Thêm sinh viên |
| **204** No Content | Thành công, không cần trả dữ liệu | Sửa, xóa |
| **400** Bad Request | Dữ liệu gửi lên sai | Tuổi 10, thiếu RowVersion, sai mật khẩu |
| **401** Unauthorized | Chưa đăng nhập / token hỏng, hết hạn | Gọi API không có token |
| **403** Forbidden | Đã đăng nhập nhưng không đủ quyền | Giảng viên thêm sinh viên |
| **404** Not Found | Không tìm thấy | Sinh viên không tồn tại / đã xóa mềm |
| **409** Conflict | Xung đột với dữ liệu hiện có | Email trùng, tài khoản trùng, đụng độ RowVersion |
| **500** Internal Server Error | Lỗi hệ thống bất ngờ | Mất kết nối DB… |

---

# PHẦN 4 – TỪNG YÊU CẦU TUẦN 1 → TUẦN 7

## ═════ TUẦN 1 – NỀN TẢNG ═════

### YC 1. DTO và kiểm tra dữ liệu đầu vào

📘 **Khái niệm:** DTO (*Data Transfer Object*) là lớp **chỉ dùng để vận chuyển dữ liệu qua API**, tách khỏi **Entity** (lớp ánh xạ bảng DB).

💡 **Bản chất:** Nếu API nhận/trả thẳng Entity thì (1) lộ cột nội bộ như `IsDeleted`, (2) client gửi kèm trường không được phép và server lỡ lưu (*over-posting*), (3) dữ liệu sai vào thẳng DB. DTO là **"cửa khẩu"** chỉ cho qua đúng những gì được phép và kiểm tra ngay tại cửa.
> Ví von: Entity là hồ sơ gốc trong kho; DTO là bản photo chỉ có những trang được phép đưa ra quầy.

⚙️ **Cách hoạt động:**
1. Request đến → **Model Binding** đổ JSON vào `SinhVienDto`.
2. **Model Validation** đọc các attribute; nhờ `[ApiController]`, sai thì **tự trả 400**, hàm Controller không chạy.
3. Đọc dữ liệu: `.Select(s => new SinhVienDto {...})` – chuyển sang DTO ngay trong SQL.
4. Ghi dữ liệu: gán **từng trường** DTO → Entity.
- File: `DTOs/SinhVienDto.cs` (`[Required]`, `[EmailAddress]`, `[Range(18, 99)]`), `DangKyDto`, `DangNhapDto`.

🌐 **Chạy trên web:** đăng nhập Admin → thêm sinh viên **tuổi 10** → Toast đỏ *"Tuổi phải là số dương từ 18 đến 99!"*. F12 → Network → `GET /api/SinhVien` → JSON **không có** `isDeleted`.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| DTO | Lớp trung gian chỉ chứa dữ liệu được phép trao đổi |
| Entity | Lớp ánh xạ đúng một bảng trong DB |
| Over-posting | Client gửi thêm trường không được phép để sửa dữ liệu trái phép |
| Model Binding | ASP.NET tự đọc request đổ vào đối tượng C# |
| Model Validation | ASP.NET tự kiểm tra dữ liệu theo attribute |
| Data Annotations | Các attribute `[Required]`, `[Range]`… gắn trên thuộc tính |
| Projection (`Select`) | Chỉ lấy đúng các cột cần, chuyển sang DTO ngay trong SQL |

---

### YC 2. Không hardcode cấu hình

📘 **Khái niệm:** Đưa chuỗi kết nối, khóa bí mật… ra **file cấu hình** (`appsettings.json`) thay vì viết cứng trong code.

💡 **Bản chất:** Mỗi môi trường (máy em, máy mentor, server thật, test) có giá trị khác nhau. Để trong file thì **đổi môi trường chỉ sửa file**, không build lại; khóa bí mật không nằm lẫn trong logic.
> Ví von: muốn đổi đèn chỉ gạt công tắc, không phải đục tường đi lại dây.

⚙️ **Cách hoạt động:** ASP.NET nạp `appsettings.json`, `appsettings.Development.json`, biến môi trường… vào **một kho chung** (`IConfiguration`). Code chỉ **hỏi theo tên khóa**. Nguồn nạp sau **đè** nguồn trước.
- `appsettings.json`: `ConnectionStrings:DefaultConnection`, `JwtSettings: Secret / Issuer / Audience`.
- `Program.cs`: `GetConnectionString("DefaultConnection")`, `GetSection("JwtSettings")`.

🌐 **Chạy trên web:** (thử nghiệm) đổi tên DB trong file thành tên sai → chạy lại API → web không tải được danh sách. Sửa lại → bình thường.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| appsettings.json | File cấu hình chính của ASP.NET |
| IConfiguration | Đối tượng để đọc cấu hình theo tên khóa |
| ConnectionString | Chuỗi chứa địa chỉ máy chủ, tên DB, cách đăng nhập |
| Environment (Development/Production) | Môi trường chạy; mỗi môi trường có thể có file cấu hình riêng |
| User Secrets / biến môi trường | Nơi an toàn hơn để giữ khóa bí mật khi triển khai thật |

---

### YC 3. Lưu mật khẩu an toàn (BCrypt)

📘 **Khái niệm:** **Băm (hash)** mật khẩu trước khi lưu, dùng thuật toán **BCrypt**.

💡 **Bản chất:** Nếu lưu mật khẩu gốc, ai đọc được DB là biết mật khẩu mọi người. Hash là **một chiều** – không thể giải ngược.
> Ví von: máy xay sinh tố – ra nước ép thì dễ, biến ngược thành trái cây thì không thể.

⚙️ **Cách hoạt động:**
- **Đăng ký:** `BCrypt.HashPassword(matKhau)` → tự sinh **salt** ngẫu nhiên, băm nhiều vòng → lưu chuỗi `$2a$11$...` vào cột `PasswordHash`.
- **Đăng nhập:** `BCrypt.Verify(matKhauNhap, hashDaLuu)` → lấy salt từ chuỗi đã lưu, băm lại, so sánh.
- Sai tài khoản hay sai mật khẩu đều báo **cùng một câu** → kẻ xấu không dò được tài khoản nào tồn tại.
- File: `Controllers/XacThucController.cs` (`DangKy`, `DangNhap`).

🌐 **Chạy trên web:** vào **Đăng ký** tạo 2 tài khoản cùng mật khẩu → mở SSMS `SELECT Username, PasswordHash FROM Users` → hai chuỗi băm **khác nhau**. Đăng nhập sai tên rồi sai mật khẩu → **cùng** thông báo *"Tài khoản hoặc mật khẩu không chính xác!"*.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Hash (băm) | Biến đổi một chiều, không giải ngược được |
| Salt | Chuỗi ngẫu nhiên trộn vào → cùng mật khẩu ra hash khác nhau |
| BCrypt | Thuật toán băm mật khẩu cố tình chậm, có sẵn salt |
| Work factor | Số vòng băm (2^11), càng lớn càng chậm, càng khó dò |
| Verify | Băm lại mật khẩu nhập vào rồi so với hash đã lưu |
| User enumeration | Kiểu tấn công dò xem tài khoản nào tồn tại |

---

### YC 4. Xác thực bằng JWT

📘 **Khái niệm:** **JWT** (*JSON Web Token*) là chuỗi token **có chữ ký** do server phát sau khi đăng nhập thành công.

💡 **Bản chất:** HTTP **không nhớ** request trước. JWT là "vé ra vào" để mỗi request tự chứng minh "tôi là ai, quyền gì".
> Ví von: vé xem phim có dấu mộc – ai cũng đọc được thông tin trên vé, nhưng không ai sửa được vì dấu mộc chỉ rạp có.

⚙️ **Cách hoạt động:**
1. Đăng nhập đúng → `GenerateJwtToken` tạo token `Header.Payload.Signature`:
   - **Payload** chứa *claims*: Id, Username, **Role**, FullName, hạn 2 giờ – chỉ mã hóa Base64 → **đọc được**.
   - **Signature** = băm(Header + Payload) bằng **khóa bí mật** → **không sửa được**.
2. Angular lưu token vào `localStorage` (`AuthService.login`).
3. `jwtInterceptor` tự gắn `Authorization: Bearer <token>` vào **mọi** request.
4. Backend `AddJwtBearer` tính lại chữ ký, kiểm tra hạn, Issuer, Audience → hợp lệ thì `HttpContext.User` có thông tin người dùng.
5. `UseAuthentication()` (biết "là ai") đặt **trước** `UseAuthorization()` (xét "được làm gì").

🌐 **Chạy trên web:**
- Đăng nhập → F12 → **Application → Local Storage** thấy `token`.
- F12 → **Network** → bấm một request → **Headers** có `Authorization: Bearer eyJ...`.
- Copy token dán vào **jwt.io** → thấy role, thời hạn.
- Xóa `token` trong Local Storage → tải lại → bị đưa về trang đăng nhập.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| JWT | Token dạng `Header.Payload.Signature` |
| Claim | Một mẩu thông tin trong token (Id, Name, Role…) |
| Signature | Chữ ký chống sửa, tạo bằng khóa bí mật |
| HmacSha256 | Thuật toán ký dùng trong đồ án |
| Bearer token | Cách gửi token: header `Authorization: Bearer <token>` |
| Stateless | Server không lưu phiên đăng nhập, chỉ kiểm chữ ký |
| Authentication | Xác thực – "bạn là ai?" |
| Issuer / Audience / Lifetime | Ai phát hành / phát cho ai / còn hạn không |
| HttpInterceptor | Trạm Angular gắn token cho mọi request |

---

### YC 5. Lập trình bất đồng bộ (async/await)

📘 **Khái niệm:** Viết code **chờ** thao tác chậm (DB, file, mạng) **mà không giữ luồng xử lý**.

💡 **Bản chất:** Server có số luồng giới hạn. Nếu mỗi request giữ một luồng đứng chờ DB, nhiều người truy cập cùng lúc sẽ hết luồng → web chậm/treo.
> Ví von: phục vụ đưa phiếu cho bếp rồi đi phục vụ bàn khác, không đứng chờ món chín.

⚙️ **Cách hoạt động:** gặp `await` → luồng được **trả về** để phục vụ request khác → DB xong thì chạy tiếp phần sau. Mọi hàm truy cập DB là `async Task<...>` và dùng `ToListAsync`, `FindAsync`, `AnyAsync`, `CountAsync`, `SaveChangesAsync`.
> Lưu ý: async **không làm 1 request nhanh hơn**; nó giúp **chịu nhiều request cùng lúc**. Phía Angular, tính bất đồng bộ thể hiện qua **Observable** (`subscribe`).

🌐 **Chạy trên web:** không có thao tác riêng – thể hiện qua việc web vẫn phản hồi khi nhiều người/nhiều tab cùng thao tác.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| async / await | Từ khóa C# để viết code bất đồng bộ |
| Task | "Lời hứa" sẽ có kết quả trong tương lai |
| Thread pool | Tập luồng của server dùng để xử lý request |
| Non-blocking I/O | Chờ I/O mà không chặn luồng |
| Scalability | Khả năng chịu tải khi số người dùng tăng |

---

### YC 6. Xử lý khi server sập / đăng nhập sai

📘 **Khái niệm:** Giao diện phải **báo lỗi rõ ràng** trong mọi tình huống lỗi.

💡 **Bản chất:** người dùng không được thấy màn hình trắng, nút bấm không phản hồi, hay phải bấm 2 lần mới thấy thông báo.

⚙️ **Cách hoạt động:**
- Không nhận được phản hồi HTTP nào (server tắt, CORS chặn) → Angular trả lỗi với **`status = 0`** → báo *"Không thể kết nối đến máy chủ..."*.
- Có phản hồi lỗi → đọc `message` trong JSON.
- Hàm dùng chung: `utils/error-message.ts` → `layThongBaoLoi(err)`.
- App chạy **zoneless** → sau khi gán `errorMessage` phải gọi `cdr.detectChanges()` (trang `login.ts`), nếu không thông báo chỉ hiện ở lần bấm thứ hai.

🌐 **Chạy trên web:**
- **Tắt backend** → đăng nhập → hiện ngay *"Không thể kết nối đến máy chủ..."*.
- Bật backend, nhập sai mật khẩu → hiện ngay (lần bấm đầu) *"Tài khoản hoặc mật khẩu không chính xác!"*.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| HttpErrorResponse | Đối tượng lỗi HTTP của Angular |
| status 0 | Không có phản hồi từ server |
| Zoneless | Angular không dùng Zone.js tự phát hiện thay đổi |
| Change Detection | Cơ chế Angular vẽ lại giao diện khi dữ liệu đổi |
| ChangeDetectorRef.detectChanges() | Báo Angular vẽ lại ngay |

---

## ═════ TUẦN 2 – TÍNH NĂNG CHÍNH ═════

### YC 7. Phân trang, tìm kiếm, sắp xếp phía server

📘 **Khái niệm:** Việc chia trang, lọc theo từ khóa, sắp xếp được làm **trong SQL Server**, API chỉ trả **một trang**.

💡 **Bản chất:** nếu tải cả bảng về rồi Angular tự chia trang, bảng 100.000 dòng sẽ rất chậm, tốn mạng, tốn RAM.
> Ví von: viết phiếu order rồi đưa bếp nấu một lần, chỉ bưng ra 5 món cần ăn.

⚙️ **Cách hoạt động:**
1. Angular gửi `GET /api/SinhVien?pageNumber=2&pageSize=5&keyword=an&sortBy=hoten&isDescending=true`.
2. ASP.NET đổ vào `SinhVienQuery` (`[FromQuery]`).
3. `SinhVienService.GetAllAsync`:
   - Chặn tham số sai (trang < 1 → 1; pageSize ngoài 1–50 → 5).
   - `IQueryable` **ghép dần** điều kiện (chưa chạy SQL – *deferred execution*): lọc họ tên/email → `switch` sắp xếp.
   - `CountAsync()` → tổng dòng; `Skip/Take` → SQL `OFFSET ... FETCH NEXT ...`.
4. Trả `PagedResult` gồm `items, totalCount, pageNumber, pageSize, totalPages`.

🌐 **Chạy trên web:**
- Gõ từ khóa vào ô **Tìm kiếm** → bấm **Tìm kiếm** → chỉ còn sinh viên khớp, tự về trang 1.
- Bấm tiêu đề cột **Họ tên / Email / Tuổi** → sắp xếp; bấm lần nữa → đảo chiều.
- Bấm **Trang sau / Trang trước** → F12 Network thấy request mới với `pageNumber` khác, response chỉ có 5 dòng.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Server-side pagination | Phân trang thực hiện ở server/DB |
| IQueryable | Câu truy vấn đang được ghép, chưa chạy |
| Deferred execution | Chỉ chạy SQL khi thật sự cần kết quả |
| Skip / Take | Bỏ qua N dòng / lấy M dòng |
| OFFSET … FETCH | Câu SQL tương ứng của Skip/Take |
| AsNoTracking | Đọc không theo dõi thay đổi → nhanh hơn |
| PagedResult\<T\> | Kết quả trang dùng chung cho mọi loại dữ liệu |

---

### YC 8. Phân quyền 3 lớp

📘 **Khái niệm:** **Phân quyền (Authorization)** – quyết định người dùng được làm gì. Admin: Thêm/Sửa/Xóa + Lịch sử; Giảng viên: chỉ xem + đổi ảnh.

💡 **Bản chất:** chặn ở **3 lớp**, nhưng chỉ **lớp API** là bảo mật thật vì chạy trên server và đọc role từ **token có chữ ký**. Hai lớp ở trình duyệt có thể bị sửa bằng F12.
> Ví von: biển chỉ dẫn và lễ tân giúp tiện, nhưng **cửa quẹt thẻ** mới là an ninh.

⚙️ **Cách hoạt động:**
| Lớp | Code | Chạy ở |
|---|---|---|
| 1. Giao diện | `*ngIf="authService.hasRole('Admin')"` ẩn form, nút Sửa/Xóa, nút Lịch sử | Trình duyệt |
| 2. Route | `roleGuard(['Admin'])` cho `/lich-su`; `authGuard` cho `/sinh-vien` | Trình duyệt |
| 3. **API** | `[Authorize]` + `[Authorize(Roles = "Admin")]` | **Server** |
- `AuthService.getRole()` **giải mã JWT** để lấy role thật; nếu `role` trong localStorage bị sửa khác token → tự khôi phục.
- Không token → **401**; sai role → **403**.

🌐 **Chạy trên web:**
- Đăng nhập **Giảng viên** → không thấy form thêm, nút Sửa/Xóa, nút Lịch sử.
- Gõ thẳng `http://localhost:4200/lich-su` → bị đưa về `/sinh-vien` kèm Toast.
- F12 → Local Storage → sửa `role` thành `Admin` → tải lại → bị trả về `GiangVien`.
- Postman gọi `POST /api/SinhVien` bằng token Giảng viên → **403**.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Authorization | Phân quyền – "bạn được làm gì?" |
| Role-based (RBAC) | Phân quyền theo vai trò |
| `[Authorize(Roles = "...")]` | Chỉ cho role được liệt kê gọi API |
| Route Guard / CanActivateFn | Hàm Angular chặn vào route |
| UrlTree | Kết quả guard trả về để chuyển hướng |
| 401 vs 403 | Chưa xác thực / đã xác thực nhưng không đủ quyền |

---

### YC 9. Audit Log (Lịch sử thao tác)

📘 **Khái niệm:** Nhật ký ghi lại **ai – làm gì – bảng nào – lúc nào – giá trị cũ/mới**.

💡 **Bản chất:** truy vết được khi dữ liệu bị sửa sai; chỉ Admin xem.
> Ví von: sổ giao ca – mọi thay đổi đều ký tên, ghi giờ, ghi "trước – sau".

⚙️ **Cách hoạt động:** bảng `AuditLogs` (`Username, Action, TableName, OldValues, NewValues, Timestamp`); giá trị cũ/mới lưu **JSON** nên một bảng log dùng cho mọi bảng. `AuditLogController.GetAll` (chỉ Admin) trả `AuditLogDto` mới nhất trước → trang `/lich-su`. (Việc ghi được tự động hóa ở YC 13.)

🌐 **Chạy trên web:** Admin sửa tuổi một sinh viên → bấm **📜 Lịch sử hệ thống** → dòng đầu: tên Admin, hành động **Sửa**, cũ `Tuoi: 20`, mới `Tuoi: 21`.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Audit trail | Dấu vết kiểm toán các thay đổi |
| OldValues / NewValues | Giá trị trước / sau, lưu dạng JSON |
| Traceability | Khả năng truy vết |

---

### YC 10. Upload ảnh đại diện

📘 **Khái niệm:** Tải file ảnh từ trình duyệt lên server và gắn với sinh viên.

💡 **Bản chất:** nhận file **an toàn**: đúng loại, không quá lớn, không trùng tên, không ghi đè ảnh người khác; DB nhẹ vì chỉ lưu đường dẫn.
> Ví von: bưu điện kiểm tra và cân hàng, dán mã vận đơn mới, cất kho, chỉ ghi mã kệ vào sổ.

⚙️ **Cách hoạt động:**
1. Angular đóng gói file bằng `FormData` → gửi `multipart/form-data` với `reportProgress: true, observe: 'events'`.
2. API nhận `IFormFile` → kiểm tra: sinh viên tồn tại (404), file rỗng, đuôi `.jpg/.jpeg/.png`, ≤ 2MB (400).
3. Đặt tên file `Guid` → lưu vào `wwwroot/avatars` → DB lưu `AvatarUrl = /avatars/<guid>.png`.
4. `app.UseStaticFiles()` cho phép trình duyệt tải ảnh qua `https://localhost:7280/avatars/...`.
5. Angular nhận `HttpEventType.UploadProgress` → tính % → vẽ thanh tiến trình.

🌐 **Chạy trên web:**
- Chọn ảnh `.png` nhỏ hơn 2MB cho một sinh viên → thanh **%** chạy → ảnh hiện trong bảng.
- Chọn file `.pdf` → Toast *"Định dạng file không hợp lệ..."*; ảnh > 2MB → *"Dung lượng file quá lớn..."*.
- Mở thư mục `wwwroot\avatars` → file tên dạng Guid.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| IFormFile | Kiểu ASP.NET để nhận file upload |
| multipart/form-data | Định dạng HTTP chuyên gửi file |
| FormData | Đối tượng trình duyệt đóng gói file |
| wwwroot / UseStaticFiles | Thư mục và cơ chế phục vụ file tĩnh |
| Guid | Mã ngẫu nhiên duy nhất dùng làm tên file |
| Whitelist | Chỉ cho phép danh sách đuôi file biết trước |
| reportProgress / UploadProgress | Theo dõi tiến trình tải lên |

---

### YC 11. Chuẩn hóa lỗi + Toast

📘 **Khái niệm:** **Toast** là thông báo nổi tự biến mất. Lỗi được hiển thị **thống nhất** từ **một chỗ** (`errorInterceptor`).

💡 **Bản chất:** không để mỗi trang tự báo lỗi kiểu riêng, không báo trùng, không bỏ sót.

⚙️ **Cách hoạt động:**
- `errorInterceptor` bọc mọi response bằng `catchError` → hiện Toast bằng `layThongBaoLoi()` → **401**: đăng xuất, về `/login`; **403**: về `/sinh-vien` → `throwError` ném tiếp cho component xử lý thêm.
- `ToastService` dùng **RxJS `Subject`** phát thông báo; `ToastComponent` (đặt sẵn trong `App`) nghe, hiện 4 giây rồi tự xóa. Nếu đang chuyển trang chưa có ai nghe → cất tạm `sessionStorage` (`flashToast`).
- Trang đăng nhập/đăng ký không Toast để tránh trùng với banner lỗi.

🌐 **Chạy trên web:** thêm sinh viên trùng email → Toast đỏ góc màn hình. Xóa token rồi thao tác → tự về trang đăng nhập. F12 Console có dòng đỏ `[API details]` khi chạy Development – **là cố ý** để debug, không phải lỗi mới.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| HttpInterceptorFn | Hàm interceptor của Angular |
| catchError / throwError | Bắt lỗi / ném tiếp lỗi trong RxJS |
| Subject | Kênh phát – nghe thông báo |
| Toast | Thông báo nổi tự biến mất |
| sessionStorage | Bộ nhớ tạm theo tab trình duyệt |

---

## ═════ TUẦN 3-4 – CHUYÊN NGHIỆP HÓA ═════

### YC 12. Exception Handling tập trung (Middleware)

📘 **Khái niệm:** **Exception** là lỗi phát sinh khi chạy. **Exception Middleware** là một "trạm" đặt **đầu pipeline**, bắt **mọi** exception và trả về **một định dạng JSON duy nhất** `{ statusCode, message, details }`.

💡 **Bản chất:** trước đây mỗi chỗ trả lỗi một kiểu (body rỗng, chuỗi, object khác tên trường), lỗi bất ngờ thì lộ cả stack trace. Gom về **một cửa** → client đọc lỗi giống nhau ở mọi nơi, chi tiết kỹ thuật không lộ ra ngoài.
> Ví von: phòng tiếp nhận khiếu nại duy nhất, trả lời khách theo một mẫu thống nhất, không đưa khách xem biên bản nội bộ.

⚙️ **Cách hoạt động:**
1. Code nghiệp vụ chỉ việc **ném** exception riêng: `BadRequestException` (400), `ForbiddenException` (403), `NotFoundException` (404), `ConflictException` (409) – đều kế thừa `AppException` có sẵn `StatusCode`.
2. `ExceptionMiddleware.InvokeAsync` gọi `await _next(context)` trong `try` → exception ở Controller/Service/EF **nổi ngược lên** và rơi vào `catch`.
3. **Pattern matching** theo kiểu exception chọn mã + câu lỗi; lỗi lạ → 500 với câu chung.
4. `details` (stack trace) **chỉ có ở môi trường Development**.
5. Lỗi **không phải exception** cũng cùng định dạng: `InvalidModelStateResponseFactory` (validation 400), `UseStatusCodePages` (401/403/404 tự sinh).
6. Angular: `layThongBaoLoi` đọc `message`; `errorInterceptor` in `details` ra Console.
- Kiểm chứng: `ExceptionMiddlewareTests` (5 test dùng Moq).

🌐 **Chạy trên web:**
- Thêm sinh viên **trùng email** → Toast *"Email này đã tồn tại..."* → F12 Network: **409**, Response `{"statusCode":409,"message":"..."}`.
- Gõ ID không tồn tại trên Postman `GET /api/SinhVien/9999` → **404** JSON chuẩn.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Middleware | Trạm xử lý mà mọi request đi qua theo thứ tự |
| Pipeline order | Thứ tự các middleware – bắt lỗi phải đứng đầu |
| Global Exception Handling | Xử lý lỗi tập trung một chỗ |
| Custom Exception | Exception tự định nghĩa mang sẵn mã HTTP |
| Pattern matching (`switch`) | Chọn xử lý theo kiểu exception |
| ErrorResponse | Khuôn JSON lỗi chung |
| IsDevelopment | Kiểm tra môi trường để quyết định có trả `details` không |
| Cross-cutting concern | Việc "chung" cắt ngang mọi chức năng (lỗi, log, bảo mật) |

---

### YC 13. Audit Logging tự động bằng SaveChangesInterceptor

📘 **Khái niệm:** **Interceptor** của EF Core là "móc" mà EF tự gọi ở những thời điểm nhất định. `SaveChangesInterceptor` được gọi **ngay trước khi lưu xuống DB**.

💡 **Bản chất:** không phải viết lệnh ghi log trong từng hàm Thêm/Sửa/Xóa (dễ quên, sót) – mọi thay đổi đều đi qua `SaveChanges` nên ghi log **tự động tại đúng một chỗ**.
> Ví von: camera ở cửa kho – mọi thứ ra vào đều phải qua cửa này nên không cần nhắc từng người ghi sổ.

⚙️ **Cách hoạt động (`Data/AuditSaveChangesInterceptor.cs`):**
1. EF gọi `SavingChangesAsync` → đọc **ChangeTracker**: bản ghi nào `Added / Modified / Deleted`, mỗi cột có `OriginalValue` (cũ), `CurrentValue` (mới), `IsModified`.
2. Xác định hành động: Thêm / Sửa / Xóa (xóa mềm `IsDeleted = true` cũng ghi là **Xóa**).
3. Sửa thì **chỉ ghi cột thật sự đổi**; bỏ qua `PasswordHash`, `RowVersion`.
4. Lấy username từ JWT qua `IHttpContextAccessor`.
5. Thêm các dòng `AuditLog` vào **cùng lần lưu** → dữ liệu và log cùng thành công hoặc cùng thất bại.
- Đăng ký: `AddHttpContextAccessor()`, `AddScoped<AuditSaveChangesInterceptor>()`, `options.AddInterceptors(...)`.

🌐 **Chạy trên web:** Admin thêm → sửa → xóa một sinh viên → mở **Lịch sử hệ thống** → 3 dòng Thêm / Sửa / Xóa đúng người, đúng giờ, giá trị cũ/mới chính xác. Đăng ký tài khoản mới → log không có `PasswordHash`.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| SaveChangesInterceptor | Móc EF chạy quanh thao tác lưu |
| SavingChangesAsync | Hàm chạy **trước** khi lưu |
| ChangeTracker | Bộ theo dõi thay đổi của EF |
| EntityState | Trạng thái: Added / Modified / Deleted / Unchanged |
| OriginalValue / CurrentValue | Giá trị gốc lúc đọc / giá trị hiện tại |
| IsModified | Cột có thực sự bị đổi hay không |
| IHttpContextAccessor | Đọc request hiện tại (lấy người dùng) từ ngoài Controller |

---

### YC 14. Xóa ảnh cũ khi đổi ảnh đại diện

📘 **Khái niệm:** Dọn **file rác** – ảnh cũ không còn ai dùng – trên ổ cứng server.

💡 **Bản chất:** dọn rác nhưng **không bao giờ được làm mất ảnh đang dùng** → thứ tự thao tác là mấu chốt.
> Ví von: chuyển nhà – làm xong giấy tờ ở nhà mới rồi mới trả nhà cũ.

⚙️ **Cách hoạt động (`UploadAvatar`):**
1. Nhớ đường dẫn ảnh cũ.
2. Ghi file mới.
3. Lưu DB: **lỗi** → xóa **file mới** vừa ghi + ném lỗi (ảnh cũ vẫn còn); **thành công** → **lúc này** mới xóa **file cũ**.

🌐 **Chạy trên web:** mở thư mục `wwwroot\avatars`, ghi nhớ tên file ảnh hiện tại của một sinh viên → đổi ảnh trên web → file cũ **biến mất**, chỉ còn file mới.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Orphan file | File "mồ côi" – không còn bản ghi nào trỏ tới |
| Compensating action | Hành động "bù lại" khi một bước thất bại (xóa file mới) |
| WebRootPath | Đường dẫn thật tới `wwwroot` trên server |

---

## ═════ TUẦN 5-6 – BẢO VỆ DỮ LIỆU VÀ KIỂM CHỨNG CODE ═════

### YC 15. Xóa mềm (Soft Delete) + Global Query Filter

📘 **Khái niệm:**
- **Xóa mềm**: không xóa dòng khỏi DB, chỉ **đánh dấu** `IsDeleted = true`.
- **Global Query Filter**: điều kiện lọc khai báo **một lần**, EF tự gắn vào **mọi** truy vấn.

💡 **Bản chất:** xóa cứng thì mất vĩnh viễn, không khôi phục được, mất lịch sử. Xóa mềm giữ dữ liệu; bộ lọc toàn cục đảm bảo không chỗ nào **quên** ẩn dòng đã xóa. Phạm vi: **chỉ áp dụng cho Sinh viên**.
> Ví von: Thùng rác trên máy tính – file vẫn còn, Explorer mặc định không hiện.

⚙️ **Cách hoạt động:**
- DB: cột `IsDeleted BIT NOT NULL DEFAULT 0`.
- `DeleteAsync`: `sv.IsDeleted = true` → EF sinh **UPDATE**, không phải DELETE.
- `AppDbContext.OnModelCreating`: `HasQueryFilter(s => !s.IsDeleted)` → mọi truy vấn (`ToList`, `Count`, `Any`, `Find`) tự có `WHERE IsDeleted = 0` → xóa lần 2 trả 404.
- Muốn xem cả dòng đã xóa: `IgnoreQueryFilters()`.
- Angular **không phải sửa gì**: vẫn `DELETE /api/SinhVien/5`, vẫn nhận 204.

🌐 **Chạy trên web:** Admin bấm **Xóa** một sinh viên → biến mất khỏi danh sách, tổng số giảm 1 → mở SSMS `SELECT Id, HoTen, IsDeleted FROM SinhVien` → dòng đó **vẫn còn**, `IsDeleted = 1`.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Soft Delete / Hard Delete | Xóa mềm (đánh dấu) / xóa cứng (DELETE thật) |
| IsDeleted flag | Cột cờ đánh dấu đã xóa |
| Global Query Filter | Bộ lọc tự áp dụng cho mọi truy vấn của một bảng |
| HasQueryFilter | Hàm khai báo bộ lọc trong `OnModelCreating` |
| IgnoreQueryFilters | Tắt bộ lọc cho riêng một truy vấn |

---

### YC 16. Tách tầng Service + Unit Test

📘 **Khái niệm:**
- **Service Layer**: tầng chứa **nghiệp vụ**, tách khỏi Controller.
- **Unit Test**: test tự động kiểm tra **từng đơn vị code nhỏ** (từng hàm của Service) một cách độc lập.

💡 **Bản chất:** Controller "béo" vừa lo HTTP vừa chứa nghiệp vụ thì khó đọc, khó test. **Mỗi lớp một trách nhiệm** → Service không phụ thuộc HTTP → test được bằng cách gọi hàm trực tiếp.
> Ví von: phục vụ nhận order, bếp nấu; muốn thử món mới chỉ cần vào bếp nấu thử, không cần mở cửa đón khách.

⚙️ **Cách hoạt động:**
| Tầng | Lo việc gì |
|---|---|
| Controller | Nhận HTTP, `[Authorize]`, gọi Service, trả 201/204 |
| `SinhVienService` | Kiểm tra, đọc/ghi DB, **ném exception** khi sai |
| `AppDbContext` | Nói chuyện với DB |
- Service nhận `AppDbContext` qua **DI** (`AddScoped`) → khi test đưa vào **DB InMemory** (DB trong RAM).
- `SinhVienServiceTests` (21 ca): mỗi test một DB riêng (tên Guid), mẫu **Arrange – Act – Assert**; `[Theory]` + 9 `[InlineData]` phủ mọi kiểu sắp xếp; `Assert.ThrowsAsync<...>` kiểm tra các nhánh lỗi.

🌐 **Chạy trên web / máy:** trên web mọi chức năng **vẫn chạy y như cũ** (chứng minh tách Service không đổi hành vi – *refactor*). Trong Visual Studio: **Test → Test Explorer → Run All** → tất cả xanh.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Service Layer | Tầng nghiệp vụ |
| Separation of Concerns | Tách trách nhiệm, mỗi lớp một việc |
| Thin Controller | Controller mỏng, chỉ điều phối |
| Refactor | Sửa cấu trúc code mà không đổi hành vi |
| Unit Test | Test một đơn vị nhỏ, độc lập, nhanh |
| xUnit / `[Fact]` / `[Theory]` | Framework test / test đơn / test nhiều bộ dữ liệu |
| Arrange – Act – Assert | Chuẩn bị – Hành động – Kiểm tra |
| EF Core InMemory | DB giả trong RAM để test |
| Moq | Thư viện tạo đối tượng giả (mock) |

---

## ═════ TUẦN 7 – CHỨNG MINH TRONG ĐIỀU KIỆN THẬT ═════

### YC 17. Integration Test với TestContainers

📘 **Khái niệm:**
- **Integration Test**: test **nhiều tầng ghép lại** (HTTP → Middleware → JWT → Controller → Service → DB).
- **TestContainers**: thư viện tự bật **container Docker** (ở đây là **SQL Server 2022 thật**) khi test và tự xóa khi xong.

💡 **Bản chất:** unit test chỉ thử Service trên DB giả, không chứng minh được các tầng phối hợp đúng, cũng không có hành vi riêng của SQL Server (như rowversion). Integration test chạy **đúng đường thật, DB thật** nhưng **dùng một lần**, không đụng DB `QLSINHVIEN`.
> Ví von: lắp cả chiếc xe chạy thử, trên đoạn đường dựng riêng cho buổi thử rồi dỡ đi.

⚙️ **Cách hoạt động:**
1. `ApiFactory : WebApplicationFactory<Program>` chạy **toàn bộ API trong bộ nhớ** test.
2. `MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")` → `StartAsync()` bật container.
3. `UseSetting("ConnectionStrings:DefaultConnection", ...)` **ghi đè** chuỗi kết nối sang container (nhờ YC 2); chốt an toàn dừng nếu còn trỏ `QLSINHVIEN`.
4. `EnsureCreatedAsync()` tạo bảng; `DisposeAsync()` xóa container.
5. `IClassFixture<ApiFactory>` → cả lớp test dùng chung 1 container.
6. Helper `TaoClientAsync(role)`: đăng ký → nâng role → đăng nhập → gắn Bearer token.
7. **6 kịch bản:** tạo + đọc lại; email trùng 409; xóa mềm; 401/403; tuổi sai 400; hai người cùng sửa 409.
- `public partial class Program { }` để project test thấy lớp `Program`.

🌐 **Chạy trên máy:** bật **Docker Desktop** → Visual Studio **Test Explorer → Run All** → trong Docker Desktop tab **Containers** thấy container SQL Server bật lên rồi tự biến mất → 6 test `SinhVienApiTests` xanh.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Integration Test | Test các tầng ghép lại |
| TestContainers | Tự bật/tắt container Docker cho test |
| Docker / Container | Môi trường chạy cô lập, dùng một lần |
| WebApplicationFactory | Chạy cả API trong bộ nhớ để test |
| IAsyncLifetime | Cho xUnit gọi khởi tạo/dọn dẹp bất đồng bộ |
| IClassFixture | Dùng chung một fixture cho cả lớp test |
| UseSetting | Ghi đè cấu hình khi test |
| EnsureCreated | Tạo bảng theo Model C# |

---

### YC 18. Code Coverage ≥ 80% (tầng Service)

📘 **Khái niệm:** **Code Coverage** là tỉ lệ % code **đã được thực thi** khi chạy test.

💡 **Bản chất:** trả lời "test đã đủ chưa?" bằng **con số**, chỉ ra **chỗ chưa test**. Coverage đo **độ rộng**, không đo **độ đúng** – chất lượng nằm ở các câu `Assert`.
> Ví von: đội kiểm tra đi qua các phòng của tòa nhà – biết phòng nào chưa ai vào, nhưng không đảm bảo phòng đã vào là an toàn.

⚙️ **Cách hoạt động:**
1. **Coverlet** chèn bộ đếm vào từng dòng/nhánh của DLL → test chạy thì đếm → xuất `coverage.cobertura.xml`.
2. **ReportGenerator** đọc XML → báo cáo HTML (xanh = đã chạy, đỏ = chưa, vàng = nhánh mới đi một phía).
3. Lọc `-classfilters:"+QuanLySinhVien.Services.*"` → chỉ tính tầng Service.

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```

🌐 **Chạy trên máy:** mở `D:\TT\QuanLySinhVien\CoverageReport\index.html` bằng trình duyệt → dòng `SinhVienService`: lần đo gần nhất **Line 100%, Branch 96,8%** (nên đo lại sau lần sửa cuối của Service). Bấm vào lớp để xem từng dòng tô màu.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Code Coverage | Độ phủ của test |
| Line coverage | % dòng đã chạy |
| Branch coverage | % nhánh rẽ (`if`, `switch`) đã đi qua cả hai phía |
| Coverlet | Công cụ thu thập độ phủ |
| ReportGenerator | Công cụ xuất báo cáo HTML |
| classfilters | Lọc chỉ tính các lớp mong muốn |

---

### YC 19. Optimistic Concurrency với RowVersion

📘 **Khái niệm:** **Concurrency** là nhiều người cùng thao tác một dữ liệu. **Optimistic Concurrency** (lạc quan) = **không khóa**, chỉ **kiểm tra lúc lưu** xem dữ liệu có bị người khác đổi chưa.

💡 **Bản chất:** chống **Lost Update** – A và B cùng mở form; A lưu đổi tên; B lưu đổi tuổi nhưng form vẫn chứa tên cũ → tên A vừa sửa **bị ghi đè mất mà không ai biết**. Không dùng cách khóa (*pessimistic*) vì người dùng web có thể mở form rất lâu.
> Ví von: nộp tài liệu có số phiên bản – bản gốc của bạn là v7 mà máy chủ đã lên v8 thì bị từ chối, phải mở v8 sửa lại.

⚙️ **Cách hoạt động:**
1. Cột `RowVersion ROWVERSION` – **SQL Server tự tăng** mỗi lần dòng bị UPDATE. Model: `[Timestamp] byte[]? RowVersion`.
2. API trả RowVersion kèm mỗi sinh viên (JSON dạng chuỗi Base64); Angular `sua()` **giữ** phiên bản lúc mở form và gửi lại khi lưu.
3. `UpdateAsync`: thiếu RowVersion → 400; gán **`Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion`** (so với phiên bản lúc mở form, không phải bản vừa đọc).
4. EF sinh `UPDATE ... WHERE Id = @id AND RowVersion = @cũ` – so sánh và ghi trong **một câu SQL**.
5. Trúng 0 dòng → `DbUpdateConcurrencyException` → `ConflictException` → **409**.
6. Angular: 409 **đụng độ** → đóng form + tải lại danh sách; 409 **email trùng** → giữ form để sửa email.
- Kiểm chứng: integration test `HaiNguoiCungSua_NguoiSauBi409`.

🌐 **Chạy trên web:**
1. Mở **2 tab**, cùng đăng nhập Admin.
2. Cả 2 tab bấm **Sửa** cùng một sinh viên.
3. Tab 1 đổi họ tên → **Lưu** → thành công.
4. Tab 2 đổi tuổi → **Lưu** → Toast *"Dữ liệu sinh viên đã bị người khác thay đổi..."*, form đóng, danh sách hiện tên mới của Tab 1.
5. F12 Network ở Tab 2: request PUT **409**.

🔑 **Keyword:**
| Keyword | Giải thích |
|---|---|
| Lost Update | Thay đổi của người trước bị người sau ghi đè mất |
| Optimistic Concurrency | Không khóa, kiểm tra lúc lưu |
| Pessimistic Locking | Khóa dữ liệu khi mở để sửa |
| rowversion / `[Timestamp]` | Cột phiên bản SQL Server tự tăng / attribute báo EF |
| Concurrency token | Cột EF đưa vào `WHERE` khi UPDATE để phát hiện đụng độ |
| OriginalValue | Giá trị gốc EF dùng trong `WHERE` |
| DbUpdateConcurrencyException | Lỗi EF ném khi UPDATE trúng 0 dòng |
| 409 Conflict | Mã HTTP báo xung đột dữ liệu |

---

# PHẦN 5 – BẢNG TRA CỨU NHANH

## 5.1. Một request "Admin sửa sinh viên" đi qua những yêu cầu nào

```
[Angular]  sua() giữ rowVersion (YC19) → luu() → SinhVienService.update()
           → jwtInterceptor gắn Bearer token (YC4)
[ASP.NET]  ExceptionMiddleware bọc try/catch (YC12)
           → UseAuthentication: kiểm JWT (YC4)
           → UseAuthorization: [Authorize(Roles="Admin")] (YC8)
           → [ApiController] kiểm tra DTO (YC1)
           → SinhVienController.Update → SinhVienService.UpdateAsync (YC16)
           → OriginalValue = RowVersion (YC19) → await SaveChangesAsync (YC5)
           → AuditInterceptor ghi log (YC13) → Query Filter (YC15)
           → SQL Server (chuỗi kết nối từ appsettings – YC2)
[Angular]  204 → tải lại trang hiện tại (YC7)
           lỗi → errorInterceptor → Toast (YC11) / 409 đụng độ → đóng form (YC19)
```

## 5.2. Tổng hợp 19 yêu cầu

| Tuần | YC | Yêu cầu | Bản chất trong 1 câu | Thấy trên web ở đâu |
|---|---|---|---|---|
| 1 | 1 | DTO | Chỉ trao đổi trường được phép, kiểm tra từ cửa | Tuổi 10 → Toast 400 |
| 1 | 2 | Cấu hình | Tách giá trị theo môi trường khỏi code | (cấu hình máy chủ) |
| 1 | 3 | BCrypt | Băm một chiều + salt | SSMS: cột `PasswordHash` |
| 1 | 4 | JWT | Vé có chữ ký: đọc được, không sửa được | F12: Local Storage, header Bearer |
| 1 | 5 | async/await | Chờ I/O không giữ luồng | (hiệu năng server) |
| 1 | 6 | Server sập | Luôn có thông báo rõ ràng | Tắt backend → báo mất kết nối |
| 2 | 7 | Phân trang server | SQL chỉ trả đúng một trang | Tìm kiếm, bấm cột, chuyển trang |
| 2 | 8 | Phân quyền 3 lớp | Bảo mật thật nằm ở API | Giảng viên không thấy nút; 403 |
| 2 | 9 | Audit Log | Ai – gì – lúc nào – trước/sau | Trang Lịch sử hệ thống |
| 2 | 10 | Upload ảnh | Kiểm tra trước khi lưu, tên Guid | Thanh %, ảnh trong bảng |
| 2 | 11 | Lỗi + Toast | Một chỗ xử lý lỗi giao diện | Toast góc màn hình |
| 3-4 | 12 | Exception Middleware | Mọi lỗi qua một cửa, một định dạng | F12: JSON `{statusCode, message}` |
| 3-4 | 13 | Audit Interceptor | Log tự động ở tầng EF | Lịch sử ghi đủ Thêm/Sửa/Xóa |
| 3-4 | 14 | Xóa ảnh cũ | Lưu DB xong mới xóa file cũ | Thư mục `wwwroot\avatars` |
| 5-6 | 15 | Soft Delete | Khai báo filter một lần, áp dụng mọi nơi | Xóa trên web, SSMS vẫn còn |
| 5-6 | 16 | Service + Unit Test | Mỗi lớp một trách nhiệm → test được | Test Explorer xanh |
| 7 | 17 | Integration Test | Đường thật, DB thật, dùng một lần | Docker Desktop + Test Explorer |
| 7 | 18 | Coverage | Đo độ rộng của test | `CoverageReport\index.html` |
| 7 | 19 | Concurrency | Không khóa, so phiên bản lúc lưu | 2 tab cùng sửa → 409 |

## 5.3. Ba nguyên lý xuyên suốt đồ án

1. **Không tin client** – mọi kiểm tra quan trọng (dữ liệu, quyền, phiên bản) đều làm lại ở server.
2. **Viết một lần, áp dụng mọi nơi** – interceptor (Angular & EF), middleware, query filter, hàm `layThongBaoLoi`.
3. **Mọi tính năng đều kiểm chứng được** – unit test, integration test, coverage, và cách thử trực tiếp trên web.
