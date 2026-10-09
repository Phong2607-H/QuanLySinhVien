# VĂN TRÌNH BÀY – DỰ ÁN QUẢN LÝ SINH VIÊN (TUẦN 1 → TUẦN 7)

> Người trình bày: Nguyễn Thanh Phong
> Thời lượng bản đầy đủ: khoảng **12–15 phút** (kèm demo). Cuối file có **bản rút gọn 3 phút**.
> Ký hiệu: *[DEMO: ...]* = thao tác trên máy · *[CHUYỂN]* = câu nối sang phần sau · **in đậm** = từ khóa nên nhấn giọng.

**Chuẩn bị trước khi trình bày:**
- Bật backend (Ctrl+F5 để Visual Studio không dừng ở exception), bật Angular, bật Docker Desktop.
- Sẵn 2 tài khoản: một **Admin**, một **Giảng viên**.
- Mở sẵn: SSMS (bảng SinhVien, Users), Test Explorer, Postman/Swagger, trang jwt.io, file `CoverageReport\index.html`.

---

## LỜI MỞ ĐẦU (~1 phút)

Em chào anh/chị. Em là Nguyễn Thanh Phong. Hôm nay em xin trình bày dự án **Quản lý Sinh viên** mà em thực hiện trong 7 tuần thực tập.

Dự án gồm hai phần. **Backend** là Web API viết bằng **ASP.NET Core .NET 10**, dùng **Entity Framework Core** kết nối SQL Server. **Frontend** là ứng dụng **Angular** dạng standalone, chạy chế độ zoneless. Ngoài ra có một project kiểm thử dùng **xUnit** và **TestContainers**.

Đường đi của một thao tác như sau: người dùng bấm trên giao diện Angular, interceptor gắn token rồi gửi request sang API. Ở API, request đi qua middleware bắt lỗi, rồi xác thực và phân quyền, tới Controller. Controller gọi tầng Service xử lý nghiệp vụ, Service đọc ghi dữ liệu qua DbContext xuống SQL Server. Nếu có lỗi, API trả về một định dạng JSON chung, và Angular hiển thị bằng thông báo nổi.

Em sẽ trình bày theo 4 giai đoạn: **Tuần 1-2 xây nền móng**, **Tuần 3-4 làm hệ thống chuyên nghiệp hơn**, **Tuần 5-6 bảo vệ dữ liệu và kiểm chứng code**, và **Tuần 7 chứng minh hệ thống chạy đúng trong điều kiện thật**.

---

## TUẦN 1-2 – XÂY NỀN MÓNG (~3,5 phút)

### Tuần 1 – Nền tảng kỹ thuật và bảo mật

Tuần 1 em làm 6 việc nền tảng.

Thứ nhất là **DTO**. Em không trả thẳng Entity ra ngoài mà dùng `SinhVienDto`, chỉ chứa những trường client cần. Trường nội bộ như `IsDeleted` không bao giờ lộ ra, và client cũng không thể gửi lên để sửa những trường không được phép. Trên DTO em đặt luôn các luật kiểm tra: họ tên bắt buộc, email đúng định dạng, tuổi từ 18 đến 99. Sai là API tự trả về lỗi 400.

Thứ hai, em **không viết cứng cấu hình**. Chuỗi kết nối và khóa JWT nằm trong `appsettings.json`. Nhờ vậy sau này khi viết test, em đổi sang database khác mà không phải sửa một dòng code nào.

Thứ ba là **mật khẩu**. Em băm mật khẩu bằng **BCrypt**, có salt ngẫu nhiên. Trong database chỉ có chuỗi băm, không ai đọc được mật khẩu gốc, kể cả người quản trị. Khi đăng nhập sai, dù sai tên hay sai mật khẩu, hệ thống đều báo chung một câu để không lộ tài khoản nào đang tồn tại.

Thứ tư là **JWT**. Đăng nhập đúng, server phát một token có chữ ký, chứa mã người dùng, tên và vai trò, hạn 2 giờ. Angular lưu token, và một interceptor tự gắn token vào mọi request. Backend kiểm tra chữ ký, hạn dùng, nơi phát hành. Nội dung token ai cũng đọc được, nhưng **không ai sửa được** vì sửa là sai chữ ký.

*[DEMO: đăng nhập → F12 tab Network thấy header `Authorization: Bearer ...` → dán token vào jwt.io cho thấy role và hạn dùng.]*

Thứ năm, mọi thao tác database em đều dùng **async/await**, để trong lúc chờ database, server vẫn phục vụ được người khác.

Thứ sáu là **xử lý sự cố**. Backend tắt thì giao diện báo "không kết nối được máy chủ" chứ không treo. Có một lỗi em gặp: đăng nhập sai phải bấm hai lần mới hiện thông báo. Nguyên nhân là ứng dụng chạy **zoneless** nên Angular không tự vẽ lại giao diện. Em xử lý bằng `detectChanges()`.

*[DEMO: nhập sai mật khẩu → thông báo hiện ngay.]*

### Tuần 2 – Các tính năng chính

Tuần 2 em làm các tính năng chính.

Đầu tiên là **phân trang, tìm kiếm, sắp xếp ở phía server**. Angular gửi số trang, từ khóa, cột sắp xếp. Em dùng `IQueryable` để ghép tất cả thành **một câu SQL**, SQL Server chỉ trả về đúng một trang kèm tổng số dòng. Dữ liệu có lớn đến đâu thì mỗi lần cũng chỉ tải 5 dòng. Tham số vô lý như kích thước trang quá lớn sẽ tự được sửa lại.

Thứ hai là **phân quyền 3 lớp**. Lớp giao diện ẩn nút Thêm, Sửa, Xóa với Giảng viên. Lớp route chặn gõ thẳng đường dẫn trang Lịch sử. Nhưng em hiểu hai lớp này chạy trên máy người dùng nên **không phải bảo mật thật**. Bảo mật thật nằm ở API với `[Authorize(Roles = "Admin")]`, vai trò được đọc từ token có chữ ký.

*[DEMO: đăng nhập Giảng viên → không thấy nút → F12 sửa `role` trong Local Storage thành Admin → tải lại → bị trả về Giảng viên → Postman gọi thêm sinh viên bằng token Giảng viên → 403.]*

Thứ ba là **Audit Log**: ghi lại ai thêm, sửa, xóa gì, lúc nào, giá trị cũ và mới; chỉ Admin xem được.

Thứ tư là **upload ảnh đại diện**: chỉ nhận jpg, png, tối đa 2MB, đổi tên file bằng Guid để không trùng, và hiện phần trăm tiến trình.

Cuối cùng là **báo lỗi thống nhất**: một interceptor bắt mọi lỗi, hiện thông báo nổi; hết phiên thì đưa về đăng nhập, không đủ quyền thì đưa về trang chính.

*[CHUYỂN]* Sau hai tuần, hệ thống đã chạy được. Nhưng code xử lý lỗi và ghi log vẫn đang rải rác ở từng chỗ. Tuần 3-4 em gom chúng lại.

---

## TUẦN 3-4 – LÀM HỆ THỐNG CHUYÊN NGHIỆP HƠN (~3 phút)

Tuần 3-4 có 3 yêu cầu.

**Yêu cầu một: dứt điểm xử lý lỗi.** Trước đây mỗi hàm tự trả lỗi một kiểu: chỗ trả chuỗi, chỗ trả object, chỗ trả body rỗng, lỗi bất ngờ thì lộ cả stack trace. Em làm như sau. Em tạo các exception riêng như `NotFoundException`, `BadRequestException`, `ConflictException`. Code nghiệp vụ gặp lỗi thì chỉ việc **ném ra**. Một **ExceptionMiddleware**, đặt **đầu tiên** trong pipeline, bắt tất cả và đổi thành một định dạng JSON chung gồm `statusCode`, `message`, `details`. Trường `details` chỉ có khi chạy Development, để không lộ chi tiết kỹ thuật ra ngoài. Những lỗi không phải exception như dữ liệu sai 400, chưa đăng nhập 401, không đủ quyền 403 em cũng đưa về cùng định dạng. Phía Angular chỉ cần đọc `message` là hiển thị được.

*[DEMO: thêm sinh viên trùng email → thông báo "Email này đã tồn tại..." → F12 tab Network cho thấy 409 và JSON chuẩn.]*

**Yêu cầu hai: ghi Audit Log tự động bằng SaveChangesInterceptor.** Trước đây phải viết lệnh ghi log trong từng hàm, dễ quên. Em viết một Interceptor gắn vào EF Core. Ngay trước mỗi lần `SaveChanges`, nó duyệt các bản ghi đang thay đổi, biết được thêm, sửa hay xóa, so giá trị cũ với giá trị mới, lấy tên người dùng từ token, rồi ghi vào bảng AuditLogs. Mật khẩu và RowVersion được loại ra khỏi log. Controller giờ **không còn dòng ghi log nào**.

*[DEMO: sửa tuổi một sinh viên từ 20 lên 21 → mở trang Lịch sử thấy dòng mới với giá trị cũ và mới.]*

**Yêu cầu ba: xóa ảnh cũ khi đổi ảnh đại diện.** Trước đây mỗi lần đổi ảnh, file cũ vẫn nằm lại trên ổ cứng thành rác. Em làm theo thứ tự an toàn: lưu file mới, cập nhật database, **chỉ khi lưu thành công** mới xóa file cũ. Nếu database lỗi thì xóa file mới vừa ghi, để không bao giờ mất ảnh đang dùng.

*[DEMO: đổi ảnh một sinh viên → mở thư mục `wwwroot/avatars` thấy file cũ đã biến mất.]*

*[CHUYỂN]* Đến đây hệ thống đã gọn gàng. Câu hỏi tiếp theo là: lỡ tay xóa nhầm thì sao, và làm sao chứng minh code đúng? Đó là Tuần 5-6.

---

## TUẦN 5-6 – BẢO VỆ DỮ LIỆU VÀ KIỂM CHỨNG CODE (~2,5 phút)

**Yêu cầu một: xóa mềm với Global Query Filter.** Trước đây bấm xóa là chạy lệnh DELETE, dữ liệu mất vĩnh viễn. Em thêm cột `IsDeleted`. Giờ xóa chỉ là **đổi cờ** thành true. Để không phải nhớ thêm điều kiện "chưa xóa" vào mọi câu truy vấn, em khai báo **Global Query Filter một lần** trong DbContext. EF Core tự gắn điều kiện đó vào mọi truy vấn: danh sách, đếm, tìm theo Id. Vì vậy xóa lại một sinh viên đã xóa sẽ nhận 404. Khi thật sự cần xem dữ liệu đã xóa thì dùng `IgnoreQueryFilters`. Theo phạm vi nghiệp vụ, em **chỉ áp dụng xóa mềm cho sinh viên**, không áp dụng cho tài khoản.

*[DEMO: xóa một sinh viên → biến mất trên web → mở SSMS thấy dòng đó vẫn còn với `IsDeleted = 1`.]*

**Yêu cầu hai: tách tầng Service và viết Unit Test.** Trước đây toàn bộ logic nằm trong Controller nên rất khó test. Em tách ra `SinhVienService`. Controller giờ mỗi hàm chỉ còn một hai dòng: gọi Service rồi trả mã HTTP. Service không biết gì về HTTP, sai thì ném exception, và middleware tuần trước đổi thành mã lỗi.

Nhờ tách như vậy, em viết được unit test với **xUnit** và database **InMemory**. Mỗi test tạo một database riêng trong RAM với dữ liệu mẫu, gọi thẳng Service, rồi kiểm tra kết quả theo mẫu **Arrange – Act – Assert**. Em test cả đường thành công lẫn mọi đường lỗi: không tìm thấy, email trùng không phân biệt hoa thường, Id không khớp, phân trang sai. Riêng phần sắp xếp em dùng `[Theory]` với 9 bộ dữ liệu để phủ hết các nhánh.

*[DEMO: Test Explorer → Run All → tất cả xanh. Nếu có thời gian: tạm đổi xóa mềm thành `Remove` → test `Delete_LaXoaMem` báo đỏ → sửa lại → xanh.]*

*[CHUYỂN]* Nhưng unit test chạy trên database giả trong RAM, không phải SQL Server thật, và chưa xét trường hợp nhiều người dùng cùng lúc. Tuần 7 giải quyết đúng hai điểm này.

---

## TUẦN 7 – CHỨNG MINH HỆ THỐNG CHẠY ĐÚNG TRONG ĐIỀU KIỆN THẬT (~3,5 phút)

**Yêu cầu một: xử lý đụng độ dữ liệu bằng Optimistic Concurrency.** Em bắt đầu từ một tình huống thật: hai Admin cùng mở form sửa một sinh viên. A lưu trước, B lưu sau. Form của B vẫn chứa dữ liệu cũ, nên khi B lưu, thay đổi của A **bị ghi đè mà không ai biết**. Hiện tượng này gọi là **Lost Update**.

Em chọn cách **lạc quan**: không khóa dữ liệu, chỉ kiểm tra lúc lưu. Em thêm cột `rowversion`, là số phiên bản mà **SQL Server tự tăng** mỗi lần dòng bị sửa. Angular giữ số phiên bản lúc mở form và gửi lại khi lưu. Trong Service, dòng quan trọng nhất là gán `OriginalValue` bằng phiên bản client gửi lên, để câu UPDATE có điều kiện "phiên bản vẫn là phiên bản lúc mở form". Nếu đã có người lưu trước, câu UPDATE không trúng dòng nào, EF ném lỗi, em đổi thành **409 Conflict**. Phía Angular, nếu là lỗi đụng độ thì đóng form và tải lại dữ liệu mới; còn nếu là 409 do email trùng thì giữ form cho người dùng sửa.

*[DEMO: mở 2 tab cùng Admin → cả hai bấm Sửa cùng một sinh viên → tab 1 lưu thành công → tab 2 lưu → thông báo "Dữ liệu sinh viên đã bị người khác thay đổi..." → form đóng, danh sách cập nhật.]*

**Yêu cầu hai: Integration Test với TestContainers.** Database giả trong RAM không tự tăng rowversion, nên không tái hiện được đụng độ thật. Em viết Integration Test: `WebApplicationFactory` chạy **toàn bộ API** ngay trong test, còn **TestContainers bật một SQL Server 2022 thật trong Docker**, dùng xong tự xóa. Em ghi đè chuỗi kết nối sang database trong container, và thêm chốt an toàn: nếu lỡ trỏ vào database thật thì dừng ngay.

Mỗi test đi đúng con đường thật: đăng ký, đăng nhập lấy token, gọi API qua middleware, phân quyền, Controller, Service, xuống SQL Server. Em có **6 kịch bản**: thêm rồi đọc lại, email trùng trả 409, xóa mềm, phân quyền 401 và 403, dữ liệu sai trả 400, và hai người cùng sửa thì người sau bị 409.

*[DEMO: Run integration test → Docker Desktop thấy container SQL Server bật lên rồi tự biến mất → 6 test xanh.]*

**Yêu cầu ba: đo Code Coverage.** Để trả lời câu hỏi "test đã đủ chưa", em đo bằng **Coverlet** và xuất báo cáo HTML bằng **ReportGenerator**, chỉ tính tầng Service. `SinhVienService` đạt trên 80% số dòng. Báo cáo tô đỏ những dòng chưa test để em bổ sung. Tuy vậy em hiểu coverage cao **không có nghĩa là không có lỗi**, vì nó chỉ cho biết dòng nào đã chạy; chất lượng nằm ở các câu kiểm tra Assert.

*[DEMO: mở `CoverageReport\index.html` → chỉ con số % của SinhVienService → bấm vào xem các dòng xanh, đỏ.]*

---

## KẾT LUẬN (~1 phút)

Tóm lại, qua 7 tuần em đã xây dựng hệ thống theo từng lớp:

- **Tuần 1-2** xây nền móng: bảo mật với BCrypt, JWT, phân quyền ở API; hiệu năng với phân trang phía server và async.
- **Tuần 3-4** gom các việc chung về một chỗ: middleware xử lý lỗi, interceptor ghi lịch sử, dọn file cũ.
- **Tuần 5-6** bảo vệ dữ liệu bằng xóa mềm và tách Service để viết unit test.
- **Tuần 7** chứng minh hệ thống đúng trong điều kiện thật: xử lý đụng độ, test trên SQL Server thật bằng Docker, và đo được độ phủ test.

Ba nguyên tắc em rút ra:
1. **Không tin client** – bảo mật thật luôn nằm ở server.
2. **Viết một lần, áp dụng mọi nơi** – middleware, interceptor, query filter đều theo tinh thần này.
3. **Đo được và kiểm chứng được** – mỗi tính năng đều có test hoặc có cách kiểm tra cụ thể.

Một vài điểm em có thể cải thiện tiếp: thêm API khôi phục sinh viên đã xóa, ghi thêm mã bản ghi vào Audit Log, và dùng mã lỗi riêng thay vì dựa vào câu chữ để Angular phân biệt các loại lỗi 409.

Em xin cảm ơn anh/chị đã lắng nghe. Em sẵn sàng nhận câu hỏi ạ.

---

# BẢN RÚT GỌN (~3 phút)

> Dùng khi bị giới hạn thời gian hoặc mentor yêu cầu "tóm tắt nhanh".

Em xin trình bày dự án **Quản lý Sinh viên** gồm Web API **ASP.NET Core .NET 10** với EF Core, SQL Server, và frontend **Angular** zoneless.

**Tuần 1-2**, em xây nền móng. Dữ liệu trao đổi qua **DTO** có kiểm tra đầu vào. Mật khẩu băm bằng **BCrypt**. Đăng nhập nhận **JWT** có chữ ký, Angular tự gắn token vào mọi request. Danh sách được **phân trang, tìm kiếm, sắp xếp ngay trong SQL**. Phân quyền 3 lớp, trong đó lớp thật sự bảo mật là `[Authorize(Roles = "Admin")]` ở API. Ngoài ra có ghi lịch sử thao tác, upload ảnh có kiểm tra, và báo lỗi thống nhất.

**Tuần 3-4**, em gom các việc chung về một chỗ. **ExceptionMiddleware** đổi mọi lỗi thành một định dạng JSON chung `statusCode, message, details`. **SaveChangesInterceptor** ghi lịch sử tự động, Controller không còn code ghi log. Khi đổi ảnh, file cũ được xóa sau khi lưu database thành công.

**Tuần 5-6**, em làm **xóa mềm** với **Global Query Filter** khai báo một lần, mọi truy vấn tự ẩn sinh viên đã xóa nhưng dữ liệu vẫn còn trong DB. Em tách **tầng Service** khỏi Controller và viết **unit test** bằng xUnit với database InMemory, phủ cả đường thành công lẫn đường lỗi.

**Tuần 7**, em xử lý **đụng độ dữ liệu** bằng **RowVersion**: hai người cùng sửa thì người lưu sau nhận 409 thay vì ghi đè. Em viết **6 integration test** chạy toàn bộ API trên **SQL Server thật trong Docker** nhờ TestContainers, và đo **code coverage tầng Service trên 80%**.

Nguyên tắc xuyên suốt của em là: **không tin client**, **viết một lần áp dụng mọi nơi**, và **mọi tính năng đều kiểm chứng được**. Em xin cảm ơn ạ.
