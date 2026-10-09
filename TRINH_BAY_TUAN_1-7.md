# BÀI TRÌNH BÀY DỰ ÁN QUẢN LÝ SINH VIÊN (TUẦN 1 – TUẦN 7)

**Người trình bày:** Nguyễn Thanh Phong

---

## Lời mở đầu

Em xin trình bày dự án Quản lý Sinh viên mà em đã thực hiện trong bảy tuần thực tập. Dự án gồm hai phần. Phần backend là một Web API viết bằng ASP.NET Core trên .NET 10, dùng Entity Framework Core để làm việc với cơ sở dữ liệu SQL Server tên QLSINHVIEN. Phần frontend là một ứng dụng Angular dạng standalone, chạy ở chế độ zoneless. Ngoài ra em có một project kiểm thử dùng xUnit, Moq và TestContainers.

Khi người dùng thao tác trên giao diện Angular, component gọi tới service. Request được interceptor gắn token rồi gửi sang API. Ở phía API, request đi qua một chuỗi middleware theo thứ tự: bắt lỗi, chuẩn hóa mã trạng thái, chuyển hướng HTTPS, CORS, phục vụ file tĩnh, xác thực và phân quyền, rồi mới tới Controller. Controller gọi tầng Service để xử lý nghiệp vụ, Service dùng AppDbContext để đọc ghi dữ liệu. Kết quả được trả về dưới dạng JSON. Nếu có lỗi, interceptor phía Angular sẽ đọc thông báo và hiển thị bằng Toast.

Sau đây em xin trình bày lần lượt các yêu cầu theo từng tuần.

---

## Tuần 1 – Xây dựng nền tảng

**Về DTO.** Em không trả trực tiếp Entity ra ngoài mà dùng lớp `SinhVienDto`. DTO chỉ chứa những trường client cần là Id, họ tên, email, tuổi, ảnh đại diện và RowVersion. Những trường nội bộ như `IsDeleted` hay `PasswordHash` không bao giờ được gửi ra ngoài. Khi đọc dữ liệu, em dùng `.Select` để chiếu thẳng sang DTO, nhờ vậy EF Core chỉ SELECT đúng những cột cần thiết. Khi ghi, em nhận DTO từ request rồi tự gán từng trường sang Entity, tránh việc người dùng gửi thêm trường lạ để sửa dữ liệu không được phép. Trên DTO em cũng đặt các ràng buộc như `[Required]`, `[EmailAddress]` và `[Range(18, 99)]` để kiểm tra dữ liệu đầu vào.

**Về cấu hình.** Em không viết cứng chuỗi kết nối hay khóa bí mật trong code mà đưa chúng vào file `appsettings.json`, rồi đọc ra bằng `IConfiguration`. Cách làm này giúp thay đổi môi trường mà không cần sửa code. Sau này khi làm integration test, em cũng tận dụng điều này để ghi đè chuỗi kết nối sang một cơ sở dữ liệu tạm trong Docker.

**Về bảo mật mật khẩu.** Khi người dùng đăng ký, em kiểm tra tài khoản và mật khẩu không được để trống, kiểm tra tên tài khoản chưa tồn tại, rồi băm mật khẩu bằng BCrypt trước khi lưu. BCrypt tự sinh salt ngẫu nhiên và băm nhiều vòng, nên hai người cùng mật khẩu vẫn cho ra hai chuỗi khác nhau, và không thể giải ngược ra mật khẩu gốc. Khi đăng nhập, em dùng `BCrypt.Verify` để so sánh. Nếu sai tài khoản hoặc sai mật khẩu, hệ thống đều trả về cùng một câu thông báo, để kẻ xấu không đoán được tài khoản nào có tồn tại.

**Về JWT.** Khi đăng nhập thành công, hàm `GenerateJwtToken` tạo ra một token chứa các thông tin gồm mã người dùng, tên đăng nhập, vai trò và họ tên, có hạn hai giờ và được ký bằng thuật toán HmacSha256. Angular lưu token này lại, và `jwtInterceptor` tự động gắn header `Authorization: Bearer` vào mọi request. Phía backend kiểm tra chữ ký, nơi phát hành, đối tượng nhận và thời hạn của token. Token hợp lệ thì thông tin người dùng, trong đó có vai trò, được đưa vào `HttpContext.User` để dùng cho việc phân quyền.

**Về bất đồng bộ.** Mọi thao tác với cơ sở dữ liệu em đều dùng phiên bản bất đồng bộ như `ToListAsync`, `FindAsync`, `SaveChangesAsync` kết hợp với `await`. Trong lúc chờ cơ sở dữ liệu trả kết quả, luồng xử lý được trả lại cho server để phục vụ request khác, giúp ứng dụng chịu tải tốt hơn.

**Về xử lý khi có sự cố.** Em viết `errorInterceptor` để bắt mọi lỗi HTTP ở một chỗ. Nếu server không phản hồi, người dùng nhận được thông báo mất kết nối thay vì một màn hình trắng. Khi đăng nhập sai, em gặp tình huống phải bấm hai lần mới thấy thông báo. Nguyên nhân là ứng dụng chạy chế độ zoneless nên Angular không tự cập nhật giao diện sau khi request trả về. Em xử lý bằng cách gọi `ChangeDetectorRef.detectChanges()` để thông báo hiện ra ngay lần bấm đầu tiên.

---

## Tuần 2 – Các tính năng chính

**Về phân trang, tìm kiếm và sắp xếp.** Em xử lý ba việc này ở phía server để không phải tải toàn bộ bảng về trình duyệt. Angular gửi lên số trang, kích thước trang, từ khóa, cột sắp xếp và chiều sắp xếp. Phía API nhận vào lớp `SinhVienQuery`. Trước tiên em chặn các giá trị không hợp lệ, ví dụ số trang nhỏ hơn 1 thì đưa về 1, kích thước trang ngoài khoảng 1 đến 50 thì đưa về 5. Sau đó em xây truy vấn bằng `IQueryable`: thêm điều kiện lọc theo họ tên hoặc email, rồi dùng biểu thức `switch` để chọn cách sắp xếp. Vì `IQueryable` chưa chạy ngay, toàn bộ điều kiện được gộp thành một câu SQL duy nhất. Em đếm tổng số dòng để tính số trang, rồi dùng `Skip` và `Take`, mà SQL Server dịch thành `OFFSET` và `FETCH NEXT`, để lấy đúng một trang. Kết quả trả về là `PagedResult` gồm danh sách, tổng số dòng, trang hiện tại và tổng số trang.

**Về phân quyền.** Em làm phân quyền ở ba lớp. Ở giao diện, em dùng `*ngIf` để ẩn các nút Thêm, Sửa, Xóa với tài khoản không phải Admin. Ở tầng định tuyến, em viết `roleGuard` để chặn người dùng gõ thẳng đường dẫn vào trang Lịch sử. Hai lớp này chỉ giúp giao diện gọn gàng và không phải bảo mật thật, vì người dùng vẫn có thể gọi API bằng Postman. Lớp bảo mật thật nằm ở API, nơi em đặt `[Authorize(Roles = "Admin")]` lên các hành động thêm, sửa, xóa. Khi một tài khoản Giảng viên cố gọi, API trả về mã 403.

**Về Audit Log.** Em tạo bảng `AuditLogs` để ghi lại ai đã thêm, sửa hay xóa dữ liệu gì, vào lúc nào, cùng với giá trị cũ và giá trị mới. Trang Lịch sử chỉ Admin mới xem được. Ở tuần 3–4, em chuyển việc ghi log này sang dạng tự động bằng Interceptor.

**Về upload ảnh đại diện.** Khi nhận file, backend kiểm tra sinh viên có tồn tại không, file có rỗng không, đuôi file có phải jpg, jpeg hoặc png không, và dung lượng có vượt quá 2MB không. Nếu hợp lệ, file được lưu vào thư mục `wwwroot/avatars` với tên là một Guid để tránh trùng tên và tránh bị đoán đường dẫn. Phía Angular, em gửi file bằng `FormData` với tùy chọn theo dõi tiến trình, nhờ vậy giao diện hiển thị được thanh phần trăm trong lúc tải lên.

**Về chuẩn hóa lỗi.** Mọi lỗi từ backend đều được trả về cùng một định dạng gồm `statusCode`, `message` và `details`. Phía Angular chỉ cần đọc `message` để hiển thị bằng Toast. Với lỗi 401, hệ thống tự đăng xuất và chuyển về trang đăng nhập. Với lỗi 403, hệ thống chuyển về trang danh sách sinh viên. Em cũng bỏ việc hiện thông báo lỗi trong từng component để tránh một lỗi hiện hai thông báo.

---

## Tuần 3–4 – Hoàn thiện xử lý lỗi, Audit và quản lý file

**Về Exception Middleware.** Em tạo một lớp cơ sở `AppException` và các lớp con là `BadRequestException`, `ForbiddenException`, `NotFoundException` và `ConflictException`, mỗi lớp gắn với một mã HTTP. Nhờ vậy, ở Controller và Service em chỉ cần ném ra exception phù hợp, không phải tự xây dựng response. `ExceptionMiddleware` đứng đầu pipeline, bọc toàn bộ phần phía sau trong một khối `try/catch`. Khi bắt được lỗi, nó kiểm tra response đã bắt đầu gửi chưa. Nếu đã gửi thì không thể đổi mã trạng thái nữa, nên nó chỉ ghi log rồi ném lại. Lỗi nghiệp vụ được ghi ở mức cảnh báo, còn lỗi hệ thống được ghi ở mức lỗi để dễ phát hiện sự cố thật. Sau đó middleware dùng `switch` để ánh xạ từng loại exception sang mã HTTP và câu thông báo. Em đặt `DbUpdateConcurrencyException` lên trước `DbUpdateException` vì đây là lớp con, nếu đặt sau sẽ không bao giờ được bắt riêng. Phần chi tiết lỗi chỉ hiển thị ở môi trường phát triển, còn ở môi trường thật thì để trống để không lộ thông tin nội bộ.

Với lỗi kiểm tra dữ liệu xảy ra trước khi vào Controller, em cấu hình `InvalidModelStateResponseFactory` để trả về cùng định dạng. Với các mã 401, 403 và 404 không phát sinh từ exception, em dùng `UseStatusCodePages` để vẫn trả về JSON chuẩn.

**Về Audit bằng Interceptor.** Em viết lớp `AuditSaveChangesInterceptor` kế thừa `SaveChangesInterceptor` và ghi đè hàm `SavingChangesAsync`, là hàm chạy ngay trước khi dữ liệu được lưu. Trong hàm này, em duyệt các thay đổi mà `ChangeTracker` đang theo dõi, bỏ qua chính bảng AuditLog và những đối tượng không thay đổi. Từ trạng thái của đối tượng, em xác định hành động là Thêm, Sửa hay Xóa, trong đó xóa mềm được nhận biết khi `IsDeleted` chuyển sang true. Em lấy giá trị cũ và giá trị mới, loại bỏ các trường nhạy cảm như `PasswordHash` và `RowVersion`, và lấy tên người thực hiện qua `IHttpContextAccessor`. Dòng log được thêm vào cùng lần lưu, nên dữ liệu và nhật ký luôn được lưu cùng nhau. Nhờ cách này, Controller không còn một dòng code ghi log nào.

**Về xóa ảnh cũ.** Khi sinh viên đổi ảnh, em lưu lại đường dẫn ảnh cũ, ghi file mới, rồi cập nhật cơ sở dữ liệu. Chỉ khi cơ sở dữ liệu lưu thành công em mới xóa file ảnh cũ. Nếu lưu thất bại, em xóa file mới vừa ghi. Thứ tự này đảm bảo không bao giờ mất ảnh đang dùng và không để lại file rác trên server.

---

## Tuần 5–6 – Xóa mềm, tầng Service và Unit Test

**Về xóa mềm.** Thay vì xóa hẳn dữ liệu, em thêm cột `IsDeleted` vào bảng. Khi người dùng bấm xóa, hệ thống chỉ đặt `IsDeleted` bằng true, nên câu lệnh thực tế là UPDATE chứ không phải DELETE. Để không phải viết điều kiện lọc ở mọi nơi, em cấu hình Global Query Filter trong `OnModelCreating`. Từ đó EF Core tự thêm điều kiện `IsDeleted = 0` vào mọi truy vấn, dữ liệu đã xóa tự động bị ẩn. Khi cần xem cả dữ liệu đã xóa, em dùng `IgnoreQueryFilters`. Cách làm này giúp dữ liệu có thể khôi phục và lịch sử không bị mất.

**Về tầng Service.** Em tách toàn bộ logic nghiệp vụ từ Controller sang lớp `SinhVienService`, gồm các hàm lấy danh sách, lấy theo Id, thêm, sửa và xóa. Phần lặp lại "tìm sinh viên, không thấy thì báo lỗi 404" được gom vào một hàm dùng chung. Em cũng bỏ dòng gán trạng thái `Modified` thủ công, vì EF Core đang theo dõi đối tượng nên tự biết cột nào thay đổi. Gán thủ công sẽ khiến EF cập nhật toàn bộ các cột và ghi Audit sai. Sau khi tách, Controller chỉ còn nhận request, gọi Service và trả mã HTTP. Em chạy lại các integration test để chứng minh việc tách tầng không làm thay đổi hành vi của API.

**Về Unit Test.** Em viết unit test cho tầng Service bằng xUnit với cơ sở dữ liệu InMemory của EF Core. Mỗi test tạo một cơ sở dữ liệu riêng có tên ngẫu nhiên để các test không ảnh hưởng nhau. Các test viết theo mẫu Arrange, Act, Assert. Với chức năng sắp xếp có tám nhánh, em dùng `[Theory]` với nhiều bộ dữ liệu để một hàm test chạy qua tất cả các nhánh. Mỗi chức năng đều có test cho trường hợp đúng và trường hợp phải báo lỗi, kiểm tra bằng `Assert.ThrowsAsync`. Ngoài ra em có năm test cho Exception Middleware, kiểm tra từng loại lỗi trả về đúng mã và đúng định dạng, và ở môi trường thật thì phần chi tiết lỗi bị ẩn.

---

## Tuần 7 – Kiểm thử nâng cao và chất lượng code

**Về Integration Test với TestContainers.** Unit test dùng cơ sở dữ liệu giả nên không phát hiện được các lỗi chỉ xuất hiện trên SQL Server thật. Vì vậy em viết thêm integration test, kiểm tra trọn luồng từ HTTP qua middleware, Controller, Service, EF Core xuống tận SQL Server. Em tạo lớp `ApiFactory` kế thừa `WebApplicationFactory` để dựng toàn bộ API trong bộ nhớ. Trước khi chạy test, `ApiFactory` dùng TestContainers bật một SQL Server trong Docker, ghi đè chuỗi kết nối sang cơ sở dữ liệu này, rồi tạo bảng theo Model. Em đặt thêm một chốt an toàn: nếu chuỗi kết nối vẫn trỏ vào QLSINHVIEN thì dừng ngay để không làm bẩn dữ liệu thật. Khi chạy xong, container tự bị xóa. Cả lớp test dùng chung một container để tiết kiệm thời gian, và dữ liệu test được sinh ngẫu nhiên để các test độc lập.

Em viết sáu kịch bản. Thứ nhất, Admin tạo sinh viên rồi đọc lại, kiểm tra mã 201 và dữ liệu khớp. Thứ hai, tạo sinh viên trùng email phải nhận mã 409. Thứ ba, xóa sinh viên thì API trả 204, đọc lại nhận 404, nhưng kiểm tra thẳng trong cơ sở dữ liệu thì dòng vẫn còn với `IsDeleted` bằng true. Thứ tư, chưa đăng nhập nhận 401, Giảng viên thêm sinh viên nhận 403. Thứ năm, nhập tuổi không hợp lệ nhận 400 kèm câu thông báo tiếng Việt. Thứ sáu là kịch bản hai người cùng sửa, em sẽ trình bày ở phần cuối.

**Về Code Coverage.** Em dùng Coverlet để đo tỉ lệ code được chạy qua khi chạy test, và ReportGenerator để xuất báo cáo HTML, chỉ tính riêng tầng Service vì đây là nơi chứa logic nghiệp vụ. Kết quả tầng Service đạt trên 80% số dòng. Trong báo cáo, dòng màu xanh là đã được test, màu đỏ là chưa được test, màu vàng là nhánh mới chạy một phần, nên em biết chính xác cần viết thêm test ở đâu. Em cũng hiểu rằng coverage cao chưa chắc code đã đúng, vì nó chỉ cho biết dòng nào đã được chạy qua. Giá trị thật của test nằm ở các câu Assert kiểm tra kết quả, và em chú ý cả branch coverage chứ không chỉ line coverage.

**Về Optimistic Concurrency.** Bài toán đặt ra là khi hai người cùng mở form sửa một sinh viên, người lưu sau sẽ ghi đè mất thay đổi của người lưu trước mà không ai biết. Em giải quyết bằng cột `RowVersion` kiểu rowversion của SQL Server. Cột này được SQL Server tự đổi giá trị mỗi khi dòng bị sửa. Trong Model, em đánh dấu thuộc tính này bằng `[Timestamp]`, nhờ đó EF Core tự thêm điều kiện so sánh RowVersion vào mọi câu UPDATE. Client nhận RowVersion khi đọc dữ liệu, giữ lại khi mở form và gửi lại khi lưu.

Trong hàm cập nhật ở Service, nếu thiếu RowVersion em trả về lỗi 400. Nếu có, em gán RowVersion của client vào `OriginalValue`. Đây là điểm quan trọng, vì nếu không gán, EF sẽ dùng phiên bản vừa đọc từ cơ sở dữ liệu, lúc nào cũng khớp, và việc kiểm tra trở nên vô nghĩa. Khi người khác đã sửa trước, RowVersion trong cơ sở dữ liệu đã thay đổi, câu UPDATE không tìm thấy dòng nào, EF ném ra `DbUpdateConcurrencyException`, và em đổi nó thành `ConflictException` với mã 409. Phía Angular nhận 409 thì hiển thị thông báo, đóng form và tải lại danh sách để người dùng sửa trên dữ liệu mới nhất. Kịch bản thứ sáu của integration test chứng minh điều này: người lưu trước nhận 204, người lưu sau nhận 409, và dữ liệu cuối cùng là của người lưu trước.

---

## Kết luận

Qua bảy tuần, em đã xây dựng một ứng dụng quản lý sinh viên có đầy đủ chức năng thêm, sửa, xóa, tìm kiếm, phân trang và upload ảnh, được bảo vệ bằng JWT và phân quyền theo vai trò. Em xử lý lỗi tập trung bằng middleware, ghi nhật ký tự động bằng interceptor, xóa mềm bằng Global Query Filter, và chống ghi đè dữ liệu bằng RowVersion. Về chất lượng, dự án có unit test cho tầng Service và middleware, integration test chạy trên SQL Server thật trong Docker, và báo cáo code coverage cho tầng Service đạt trên 80%.

Điều em học được nhiều nhất là cách tách trách nhiệm rõ ràng giữa các tầng, xử lý những việc chung như lỗi và nhật ký ở một nơi duy nhất, và dùng kiểm thử tự động để tự tin khi thay đổi code.

Em xin cảm ơn anh/chị đã lắng nghe.
