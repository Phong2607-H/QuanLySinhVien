namespace QuanLySinhVien.Exceptions
{
    public abstract class AppException : Exception
    {
        public int StatusCode { get; }
        protected AppException(string message, int statusCode) : base(message) => StatusCode = statusCode;
    }
    public class BadRequestException : AppException { public BadRequestException(string m) : base(m, 400) { } }
    public class ForbiddenException : AppException { public ForbiddenException(string m) : base(m, 403) { } }
    public class NotFoundException : AppException { public NotFoundException(string m) : base(m, 404) { } }
    public class ConflictException : AppException { public ConflictException(string m) : base(m, 409) { } }
}