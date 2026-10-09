# BỘ CÂU HỎI VẤN ĐÁP – DỰ ÁN QUẢN LÝ SINH VIÊN (TUẦN 1 → TUẦN 7)

> Người chuẩn bị: Nguyễn Thanh Phong
> Mỗi câu trả lời bám vào code thật của dự án. Phần **"Trả lời"** là câu nói ngắn gọn khi mentor hỏi; phần **"Code"** là nơi chỉ cho mentor xem nếu được yêu cầu.

---

## Mục lục

- [A. Tổng quan và kiến trúc](#a-tổng-quan-và-kiến-trúc) (Câu 1–5)
- [B. Tuần 1 – Nền tảng](#b-tuần-1--nền-tảng) (Câu 6–18)
- [C. Tuần 2 – Tính năng chính](#c-tuần-2--tính-năng-chính) (Câu 19–33)
- [D. Tuần 3-4 – Exception, Audit, quản lý file](#d-tuần-3-4--exception-audit-quản-lý-file) (Câu 34–44)
- [E. Tuần 5-6 – Soft-Delete, Service, Unit Test](#e-tuần-5-6--soft-delete-service-unit-test) (Câu 45–55)
- [F. Tuần 7 – Integration Test, Coverage, Concurrency](#f-tuần-7--integration-test-coverage-concurrency) (Câu 56–70)
- [G. Câu hỏi tình huống và mở rộng](#g-câu-hỏi-tình-huống-và-mở-rộng) (Câu 71–80)

---

## A. Tổng quan và kiến trúc

**1. Em mô tả ngắn gọn kiến trúc dự án?**
Trả lời: Dự án gồm Web API ASP.NET Core .NET 10 dùng EF Core kết nối SQL Server, frontend Angular standalone chạy zoneless, và project test dùng xUnit, Moq, TestContainers. Backend chia thành Controller nhận request, Service xử lý nghiệp vụ, AppDbContext làm việc với DB, cùng Middleware và Interceptor xử lý việc chung như lỗi và nhật ký.

**2. Một request từ Angular đi qua những bước nào đến DB?**
Trả lời: Component gọi service, `jwtInterceptor` gắn token, request tới API và đi qua ExceptionMiddleware, UseStatusCodePages, HTTPS, CORS, StaticFiles, Authentication, Authorization rồi tới Controller. Controller gọi Service, Service dùng AppDbContext, EF sinh SQL gửi tới SQL Server. Kết quả trả về JSON; nếu lỗi thì `errorInterceptor` hiện Toast.

**3. Vì sao ExceptionMiddleware phải đặt đầu tiên trong pipeline?**
Trả lời: Middleware chỉ bắt được lỗi của những phần chạy **sau** nó. Đặt đầu tiên thì nó bọc toàn bộ pipeline, kể cả lỗi từ Authentication hay Controller đều được trả về JSON chuẩn.
Code: `Program.cs` – `app.UseMiddleware<ExceptionMiddleware>()` là dòng đầu sau `builder.Build()`.

**4. Vì sao `UseAuthentication` phải đứng trước `UseAuthorization`?**
Trả lời: Authentication xác định "người này là ai" từ JWT và gắn vào `HttpContext.User`. Authorization dựa vào thông tin đó để quyết định "có được làm không". Đảo thứ tự thì Authorization không biết người dùng là ai và luôn chặn.

**5. CORS là gì, dự án cấu hình thế nào?**
Trả lời: Trình duyệt chặn trang web gọi API khác nguồn (khác domain hoặc cổng). Angular chạy ở `localhost:4200`, API ở `localhost:7280` nên cần CORS. Em tạo policy `AllowAngular` chỉ cho phép `http://localhost:4200`, mọi header và method, không mở cho tất cả mọi nguồn.

---

## B. Tuần 1 – Nền tảng

**6. DTO là gì, vì sao không trả Entity trực tiếp?**
Trả lời: DTO là đối tượng chỉ chứa dữ liệu cần truyền. Trả Entity sẽ lộ trường nội bộ như `IsDeleted`, `PasswordHash`, và client có thể gửi thêm trường lạ để sửa dữ liệu không được phép (over-posting). Em dùng `SinhVienDto` cho cả đọc và ghi.

**7. Em ánh xạ Entity sang DTO bằng cách nào? Vì sao không dùng AutoMapper?**
Trả lời: Em dùng `.Select(s => new SinhVienDto {...})` để EF chỉ SELECT đúng các cột cần. Dự án nhỏ, ít DTO nên viết tay rõ ràng hơn và không cần thêm thư viện.

**8. Validation dữ liệu đầu vào được làm ở đâu?**
Trả lời: Bằng attribute trên DTO: `[Required]` cho họ tên và email, `[EmailAddress]`, `[Range(18, 99)]` cho tuổi. Nhờ `[ApiController]`, ASP.NET tự kiểm tra trước khi vào Controller, sai thì `InvalidModelStateResponseFactory` trả 400 với câu lỗi tiếng Việt.

**9. Vì sao không viết cứng connection string và khóa JWT trong code?**
Trả lời: Để đổi môi trường mà không sửa code, và tránh lộ bí mật khi đưa code lên Git. Em để trong `appsettings.json` và đọc bằng `IConfiguration`. Integration test cũng nhờ vậy mà ghi đè được connection string sang DB trong Docker.

**10. Trong thực tế khóa JWT nên để ở đâu?**
Trả lời: Không nên để trong `appsettings.json` đưa lên Git. Lúc phát triển dùng User Secrets, khi chạy thật dùng biến môi trường hoặc dịch vụ quản lý bí mật như Azure Key Vault. Trong dự án thực tập em để trong `appsettings.json` cho đơn giản.

**11. Hash khác mã hóa (encrypt) thế nào? Vì sao dùng BCrypt?**
Trả lời: Mã hóa có thể giải ngược bằng khóa, còn hash là một chiều, không giải ngược được. Mật khẩu chỉ cần so sánh, không cần đọc lại, nên dùng hash. BCrypt tự sinh **salt** ngẫu nhiên và băm nhiều vòng, chống được tấn công bảng tra sẵn (rainbow table) và brute-force tốt hơn MD5 hay SHA thường.

**12. Hai người cùng mật khẩu thì hash có giống nhau không?**
Trả lời: Không. Mỗi lần `BCrypt.HashPassword` sinh salt khác nhau, nên cùng mật khẩu ra hai chuỗi khác nhau. Salt được lưu ngay trong chuỗi hash, nên `BCrypt.Verify` vẫn so sánh được.

**13. Vì sao sai tài khoản và sai mật khẩu lại báo cùng một câu?**
Trả lời: Nếu báo riêng "tài khoản không tồn tại", kẻ xấu biết được tài khoản nào có thật để tập trung đoán mật khẩu. Em trả chung "Tài khoản hoặc mật khẩu không chính xác!".

**14. JWT gồm những phần nào? Token của em chứa gì?**
Trả lời: Ba phần `header.payload.signature`. Payload của em có `NameIdentifier` (Id), `Name` (username), `Role`, `FullName`, `exp` hết hạn sau 2 giờ. Chữ ký tạo bằng HmacSha256 với khóa bí mật của server.
Code: `XacThucController.GenerateJwtToken`.

**15. Payload JWT có bị mã hóa không? Có nên để mật khẩu vào đó không?**
Trả lời: Không, payload chỉ là base64, ai cũng đọc được. Chữ ký chỉ chống **sửa**, không chống **đọc**. Vì vậy tuyệt đối không để mật khẩu hay dữ liệu nhạy cảm trong token.

**16. Backend kiểm tra token thế nào?**
Trả lời: `AddJwtBearer` với `TokenValidationParameters` bật kiểm tra Issuer, Audience, thời hạn và chữ ký bằng `SymmetricSecurityKey`. Sai bất kỳ điều kiện nào thì trả 401.

**17. async/await giúp gì? Có làm request nhanh hơn không?**
Trả lời: Không làm một request nhanh hơn, mà giúp server phục vụ **nhiều request hơn**. Trong lúc chờ DB, luồng được trả về để xử lý request khác thay vì đứng chờ. Em dùng `ToListAsync`, `FindAsync`, `SaveChangesAsync`.

**18. Vì sao trang đăng nhập phải gọi `detectChanges()`?**
Trả lời: Ứng dụng chạy zoneless nên Angular không tự cập nhật giao diện sau khi request bất đồng bộ trả về. Không gọi thì phải bấm lần hai mới thấy thông báo. Em gọi `ChangeDetectorRef.detectChanges()` để giao diện cập nhật ngay.

---

## C. Tuần 2 – Tính năng chính

**19. Vì sao phân trang ở server mà không ở Angular?**
Trả lời: Phân trang ở client phải tải toàn bộ bảng về, tốn băng thông và chậm khi dữ liệu lớn. Ở server, SQL Server chỉ trả đúng 5 dòng của trang hiện tại.

**20. IQueryable khác IEnumerable thế nào?**
Trả lời: `IQueryable` xây biểu thức truy vấn và chỉ chạy khi gọi `ToListAsync`, `CountAsync`; toàn bộ `Where`, `OrderBy`, `Skip`, `Take` được dịch thành **một câu SQL**. `IEnumerable` xử lý trong bộ nhớ, tức là kéo hết dữ liệu về rồi mới lọc.

**21. Deferred execution là gì? Trong code của em nó thể hiện ở đâu?**
Trả lời: Truy vấn chỉ thực thi khi thật sự cần kết quả. Trong `GetAllAsync`, em nối thêm `Where` và `OrderBy` vào biến `queryable` nhiều lần nhưng chưa chạy SQL; chỉ khi gọi `CountAsync()` và `ToListAsync()` mới gửi tới DB.

**22. Skip/Take được dịch thành SQL gì?**
Trả lời: `OFFSET (page-1)*size ROWS FETCH NEXT size ROWS ONLY`. Bắt buộc phải có `OrderBy` trước, nên em luôn có nhánh mặc định sắp xếp theo `Id`.

**23. Nếu người dùng gửi pageSize = 100000 thì sao?**
Trả lời: Em chặn trong Service: `PageSize` nhỏ hơn 1 hoặc lớn hơn 50 thì đưa về 5, `PageNumber` nhỏ hơn 1 thì đưa về 1. Tránh việc một request kéo cả bảng làm nặng server.

**24. Tìm kiếm có phân biệt hoa thường không?**
Trả lời: Không. Em `Trim().ToLower()` từ khóa và so với `HoTen.ToLower()`, `Email.ToLower()`. Với SQL Server collation mặc định cũng không phân biệt, nhưng viết rõ giúp chạy đúng cả trên InMemory khi unit test.

**25. Vì sao dùng `AsNoTracking` trong GetAll?**
Trả lời: Danh sách chỉ để đọc, không sửa. `AsNoTracking` bảo EF không theo dõi thay đổi, đỡ tốn bộ nhớ và nhanh hơn.

**26. `TotalPages` được tính ở đâu?**
Trả lời: Trong `PagedResult<T>` là thuộc tính tính toán `Math.Ceiling(TotalCount / PageSize)`. Server trả kèm để Angular biết khi nào tắt nút "Sau".

**27. Em phân quyền ở những lớp nào?**
Trả lời: Ba lớp. Giao diện dùng `*ngIf="authService.hasRole('Admin')"` ẩn nút; định tuyến dùng `roleGuard` chặn vào `/lich-su`; API dùng `[Authorize(Roles = "Admin")]`. Chỉ lớp API là bảo mật thật.

**28. Nếu vào Local Storage sửa role thành Admin thì có thêm/sửa/xóa được không?**
Trả lời: Không. `getRole()` đọc role từ **JWT** chứ không tin Local Storage; thấy khác nhau thì ghi lại role thật và báo lỗi. Kể cả sửa payload của token để nút hiện ra, backend kiểm tra chữ ký thấy sai sẽ trả 401 và interceptor đăng xuất người dùng.

**29. Guard trả về `UrlTree` khác gì gọi `router.navigate`?**
Trả lời: Trả `UrlTree` để Router tự hủy điều hướng hiện tại và chuyển sang trang mới một cách chuẩn, không bị điều hướng chồng chéo. `navigate` trong guard rồi trả `false` dễ gây hai lần điều hướng.

**30. Giảng viên được làm gì, Admin được làm gì?**
Trả lời: Giảng viên xem danh sách và upload ảnh đại diện (`[Authorize(Roles = "Admin,GiangVien")]` trên UploadAvatar). Admin được thêm, sửa, xóa sinh viên và xem trang Lịch sử.

**31. Upload ảnh được kiểm tra những gì?**
Trả lời: Sinh viên tồn tại, file không rỗng, đuôi `.jpg/.jpeg/.png`, dung lượng tối đa 2MB. Sai thì ném `BadRequestException` hoặc `NotFoundException`.

**32. Vì sao đặt tên file bằng Guid?**
Trả lời: Tránh trùng tên khi hai người upload `avatar.jpg`, tránh ghi đè file của người khác, và tránh lộ hoặc đoán được tên file gốc.

**33. Thanh phần trăm upload hoạt động thế nào?**
Trả lời: Angular gửi `FormData` với `reportProgress: true, observe: 'events'`. HttpClient phát nhiều sự kiện; với `HttpEventType.UploadProgress` em tính `loaded / total * 100`, với `HttpEventType.Response` thì tải lại danh sách.

---

## D. Tuần 3-4 – Exception, Audit, quản lý file

**34. Vì sao tạo exception riêng như NotFoundException, ConflictException?**
Trả lời: Mỗi exception gắn với một mã HTTP. Service chỉ cần `throw new NotFoundException(...)`, không phải biết gì về HTTP. ExceptionMiddleware đọc loại exception để trả đúng mã 404, 409.

**35. Format lỗi chuẩn của em gồm những gì?**
Trả lời: `{ statusCode, message, details }`, riêng lỗi validation có thêm `errors` liệt kê lỗi từng trường. Angular chỉ cần đọc `message` để hiện Toast.

**36. `Response.HasStarted` dùng để làm gì?**
Trả lời: Nếu response đã bắt đầu gửi về client thì không thể đổi status code hay header nữa. Lúc đó middleware chỉ ghi log rồi ném lại lỗi, tránh sinh thêm lỗi thứ hai.

**37. Vì sao `DbUpdateConcurrencyException` phải đặt trước `DbUpdateException` trong switch?**
Trả lời: `DbUpdateConcurrencyException` là lớp con của `DbUpdateException`. Switch khớp theo thứ tự từ trên xuống, nếu đặt lớp cha trước thì lớp con không bao giờ được bắt riêng.

**38. Vì sao lỗi nghiệp vụ ghi LogWarning còn lỗi khác ghi LogError?**
Trả lời: Lỗi 4xx do người dùng nhập sai, không phải sự cố hệ thống. Lỗi 5xx mới là sự cố cần xử lý. Phân mức giúp khi đọc log thấy ngay lỗi thật.

**39. Vì sao `details` chỉ hiện ở Development?**
Trả lời: `details` chứa stack trace, lộ cấu trúc code và đường dẫn server. Ở Production em để `null`, điều này đã có unit test kiểm tra.

**40. Lỗi validation và lỗi 401/403 có đi qua ExceptionMiddleware không?**
Trả lời: Không. Validation bị chặn trước Controller nên em dùng `InvalidModelStateResponseFactory`. 401/403 là mã trạng thái do Authentication/Authorization trả, không phải exception, nên em dùng `UseStatusCodePages` để viết JSON.

**41. SaveChangesInterceptor là gì, vì sao dùng thay vì ghi log trong Controller?**
Trả lời: Là lớp can thiệp vào quá trình `SaveChanges` của EF. Ghi log ở Controller dễ quên và lặp code. Interceptor chạy tự động ở **mọi** lần lưu, Controller không cần dòng ghi log nào.

**42. Interceptor biết hành động là Thêm, Sửa hay Xóa bằng cách nào?**
Trả lời: Dựa vào `EntityState` trong `ChangeTracker`: `Added` là Thêm, `Modified` là Sửa. Riêng xóa mềm cũng là `Modified`, nên em kiểm tra thêm `IsDeleted` chuyển sang true thì ghi là Xóa.

**43. Làm sao Interceptor biết ai đang thao tác?**
Trả lời: Qua `IHttpContextAccessor` lấy `HttpContext.User.Identity.Name` từ JWT. Phải đăng ký `AddHttpContextAccessor()` trong `Program.cs`.

**44. Vì sao xóa ảnh cũ **sau** khi lưu DB mà không phải trước?**
Trả lời: Nếu xóa trước mà lưu DB lỗi thì DB vẫn trỏ tới ảnh đã mất. Em lưu DB thành công rồi mới xóa ảnh cũ; nếu lưu lỗi thì xóa file mới vừa ghi để không để rác.

---

## E. Tuần 5-6 – Soft-Delete, Service, Unit Test

**45. Xóa mềm khác xóa cứng thế nào? Vì sao chọn xóa mềm?**
Trả lời: Xóa cứng chạy DELETE, mất dữ liệu vĩnh viễn. Xóa mềm chỉ đặt `IsDeleted = true` bằng UPDATE. Dữ liệu còn nên khôi phục được, giữ được lịch sử và không làm hỏng dữ liệu liên quan.

**46. Global Query Filter hoạt động thế nào?**
Trả lời: Trong `OnModelCreating` em khai báo `HasQueryFilter(s => !s.IsDeleted)`. EF tự thêm `WHERE IsDeleted = 0` vào mọi truy vấn trên bảng đó, không phải nhớ viết điều kiện ở từng nơi.

**47. Muốn xem sinh viên đã xóa thì làm sao? Giảng viên có xem được không?**
Trả lời: Dùng `IgnoreQueryFilters()`. Hiện chưa có API nào dùng nó nên trên web không ai xem được, kể cả Admin; chỉ xem được bằng SQL. Nếu làm thùng rác em sẽ chỉ cho Admin truy cập.

**48. Xóa mềm có nhược điểm gì?**
Trả lời: Dữ liệu tăng dần, ràng buộc UNIQUE có thể bị vướng (email của sinh viên đã xóa vẫn còn), và truy vấn thô bằng SQL phải nhớ thêm điều kiện. Có thể khắc phục bằng index có điều kiện hoặc định kỳ dọn dữ liệu cũ.

**49. Vì sao tách tầng Service?**
Trả lời: Controller chỉ lo HTTP, Service lo nghiệp vụ. Logic dễ đọc, dễ tái sử dụng và dễ unit test vì test Service không cần dựng HTTP. Yêu cầu code coverage cũng tính cho tầng Service.

**50. Vì sao đăng ký Service là Scoped?**
Trả lời: Service dùng `AppDbContext`, mà DbContext là Scoped (mỗi request một instance). Service phải cùng vòng đời hoặc ngắn hơn, nếu là Singleton sẽ giữ DbContext của request cũ gây lỗi.

**51. Vì sao bỏ dòng `_context.Entry(sv).State = EntityState.Modified`?**
Trả lời: Entity đọc bằng `FindAsync` đã được EF theo dõi nên EF tự biết cột nào đổi. Gán `Modified` khiến EF UPDATE toàn bộ cột và Audit ghi sai như thể mọi cột đều thay đổi.

**52. Unit test khác integration test thế nào?**
Trả lời: Unit test kiểm tra một phần nhỏ với phụ thuộc giả (DB InMemory, Moq), chạy rất nhanh. Integration test ghép các phần thật lại, từ HTTP xuống SQL Server thật, chậm hơn nhưng bắt được lỗi khi ghép.

**53. Mẫu AAA là gì?**
Trả lời: Arrange chuẩn bị dữ liệu, Act gọi hàm cần test, Assert kiểm tra kết quả. Ví dụ `Create_HopLe_LuuVaoDb`: tạo DB mẫu, gọi `CreateAsync`, kiểm tra Id lớn hơn 0 và số dòng tăng lên 4.

**54. `[Fact]` khác `[Theory]` thế nào?**
Trả lời: `[Fact]` là một test cố định. `[Theory]` kèm nhiều `[InlineData]` để chạy cùng một test với nhiều bộ dữ liệu. Em dùng `[Theory]` 9 bộ dữ liệu để đi qua mọi nhánh sắp xếp.

**55. Vì sao mỗi test tạo DB InMemory với tên Guid?**
Trả lời: Để mỗi test có DB riêng, không bị dữ liệu của test khác ảnh hưởng, chạy theo thứ tự nào cũng cho cùng kết quả.

---

## F. Tuần 7 – Integration Test, Coverage, Concurrency

**56. Integration test của em kiểm tra những gì?**
Trả lời: Sáu kịch bản: tạo rồi đọc lại (201), email trùng (409), xóa mềm (204 rồi 404, DB còn dòng), phân quyền (401/403), tuổi sai (400), hai người cùng sửa (204/409).

**57. TestContainers là gì? Docker đóng vai trò gì?**
Trả lời: Thư viện tự bật một container Docker khi test chạy và tự xóa khi xong. Docker chỉ cung cấp một SQL Server thật, sạch, dùng xong bỏ. Code test và API vẫn chạy trên máy em.

**58. Vì sao không test vào DB QLSINHVIEN hoặc dùng InMemory?**
Trả lời: Test vào DB thật sẽ làm bẩn dữ liệu và kết quả không ổn định. InMemory không phải SQL Server nên không có `rowversion` tự tăng, không chạy SQL thật, bỏ sót lỗi.

**59. `WebApplicationFactory<Program>` làm gì? Vì sao phải thêm `public partial class Program {}`?**
Trả lời: Nó dựng toàn bộ API trong bộ nhớ để test gọi bằng HttpClient. `Program.cs` viết kiểu top-level statements nên class `Program` do compiler sinh ra là `internal`; dòng `partial` thêm một phần `public` để project test thấy được.

**60. Làm sao em chắc test không chạy vào DB thật?**
Trả lời: `ApiFactory` có chốt an toàn: connection string chứa `QLSINHVIEN` thì ném lỗi dừng ngay. Ngoài ra khi chạy có thể thấy container SQL Server xuất hiện trong Docker Desktop với cổng ngẫu nhiên.

**61. Vì sao dùng `IClassFixture` và dữ liệu Guid?**
Trả lời: `IClassFixture` để cả class dùng chung một container, vì bật container mất vài giây. Các test dùng chung DB nên email và username sinh bằng Guid để không trùng nhau.

**62. Test làm sao có được tài khoản Admin?**
Trả lời: Helper `TaoClientAsync` gọi API đăng ký thật, vào DB đổi Role thành Admin (API đăng ký không cho chọn Role), rồi gọi API đăng nhập lấy JWT và gắn header Bearer.

**63. Code coverage là gì? Dự án đạt bao nhiêu?**
Trả lời: Tỉ lệ code được chạy qua khi chạy test. Em dùng Coverlet đo, ReportGenerator xuất HTML, chỉ tính tầng Service. Tầng Service đạt trên 80% line coverage.

**64. Line coverage khác branch coverage thế nào?**
Trả lời: Line tính theo dòng đã chạy, branch tính theo nhánh `if/else`, `switch`. Một dòng `if (PageNumber < 1) PageNumber = 1;` có thể đạt 100% line nhưng chỉ 50% branch nếu chưa test trường hợp PageNumber nhỏ hơn 1.

**65. Coverage 100% có nghĩa là code không lỗi không?**
Trả lời: Không. Coverage chỉ cho biết dòng đã được chạy, không cho biết kết quả có đúng không. Một test không có Assert vẫn tăng coverage. Chất lượng nằm ở các Assert.

**66. Optimistic concurrency khác pessimistic thế nào? Vì sao chọn optimistic?**
Trả lời: Pessimistic khóa dữ liệu ngay khi đọc, người khác phải chờ. Optimistic không khóa, chỉ kiểm tra lúc lưu. Web có người mở form rất lâu, khóa sẽ chặn người khác, nên optimistic phù hợp hơn.

**67. RowVersion hoạt động thế nào?**
Trả lời: Là cột kiểu `rowversion` do SQL Server tự đổi giá trị mỗi lần dòng bị sửa. `[Timestamp]` báo EF thêm `AND RowVersion = @cũ` vào câu UPDATE. Người khác đã sửa trước thì giá trị đã đổi, UPDATE ảnh hưởng 0 dòng, EF ném `DbUpdateConcurrencyException`.

**68. Vì sao phải gán `OriginalValue = dto.RowVersion`?**
Trả lời: `FindAsync` đọc RowVersion **mới nhất** từ DB nên lúc nào cũng khớp. Phải gán RowVersion của lúc client **mở form** thì EF mới so sánh đúng thời điểm và phát hiện được người khác đã sửa.

**69. Client không gửi RowVersion thì sao?**
Trả lời: Service trả 400 "Thiếu RowVersion". Nếu không chặn, EF sẽ so `RowVersion IS NULL`, luôn không khớp và trả 409 khó hiểu.

**70. Khi nhận 409, Angular xử lý thế nào?**
Trả lời: `errorInterceptor` hiện Toast với câu thông báo từ server. Component gọi `lamMoiForm()` đóng form vì dữ liệu đã cũ, và `taiDanhSach()` để lấy dữ liệu mới nhất kèm RowVersion mới.

---

## G. Câu hỏi tình huống và mở rộng

**71. Token hết hạn khi đang dùng thì chuyện gì xảy ra?**
Trả lời: Backend trả 401, `errorInterceptor` gọi `logout()` xóa token và chuyển về `/login`. Muốn trải nghiệm tốt hơn có thể làm thêm Refresh Token.

**72. Lưu token trong Local Storage có rủi ro gì?**
Trả lời: Nếu trang bị lỗi XSS, script độc có thể đọc token. Cách an toàn hơn là cookie HttpOnly. Dự án dùng Local Storage cho đơn giản; em giảm rủi ro bằng thời hạn token 2 giờ.

**73. Nếu hai người cùng **xóa** một sinh viên thì sao?**
Trả lời: Người đầu xóa mềm thành công. Người sau gọi `FindAsync` không thấy vì Global Query Filter đã ẩn, nên nhận 404 "Không tìm thấy sinh viên".

**74. Có thể đăng ký hai tài khoản cùng username không?**
Trả lời: Không. Code kiểm tra `AnyAsync` trước và trả 409. Ngoài ra cột `Username` có ràng buộc UNIQUE trong DB, nên kể cả hai request đến cùng lúc thì DB cũng chặn, ExceptionMiddleware đổi `DbUpdateException` thành 409.

**75. Email sinh viên có ràng buộc UNIQUE trong DB không?**
Trả lời: Hiện em kiểm tra trùng bằng code (`AnyAsync`). Để chắc chắn hơn khi có nhiều request đồng thời, nên thêm UNIQUE index trong DB; nếu dùng xóa mềm thì nên là index có điều kiện `WHERE IsDeleted = 0`.

**76. Upload một file `.exe` đổi đuôi thành `.jpg` thì sao?**
Trả lời: Hiện em chỉ kiểm tra đuôi file nên sẽ lọt qua. Cách chặt chẽ hơn là kiểm tra vài byte đầu của file (magic number) hoặc `ContentType`. File cũng chỉ được phục vụ như ảnh tĩnh, không được thực thi.

**77. Nếu SQL Server sập thì người dùng thấy gì?**
Trả lời: EF ném exception kết nối, ExceptionMiddleware trả 500 "Đã xảy ra sự cố hệ thống!...", Angular hiện Toast. Người dùng không thấy stack trace.

**78. Lỗi N+1 là gì? Dự án có gặp không?**
Trả lời: Là khi lấy N dòng rồi mỗi dòng lại chạy thêm một truy vấn con, tổng N+1 truy vấn. Dự án dùng `.Select` sang DTO và không có quan hệ cần tải lồng nhau nên không gặp. Nếu có quan hệ, em dùng `Include` hoặc chiếu trong `Select`.

**79. Nếu làm lại, em muốn cải thiện gì?**
Trả lời: Thêm Refresh Token và cookie HttpOnly, thêm UNIQUE index cho email, kiểm tra nội dung file upload, làm chức năng thùng rác cho Admin, thêm CI chạy test tự động trên GitHub Actions.

**80. Em học được gì nhiều nhất qua dự án?**
Trả lời: Cách tách trách nhiệm giữa các tầng, xử lý việc chung như lỗi và nhật ký ở một nơi, bảo mật phải nằm ở backend, và dùng kiểm thử tự động để tự tin khi sửa code. Ví dụ khi tách tầng Service, integration test giúp em chắc chắn hành vi API không đổi.
