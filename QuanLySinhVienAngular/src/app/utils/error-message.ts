import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from '../models/api-error';

export function layThongBaoLoi(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại backend!';
    }
    const body = err.error as ApiError | null;
    if (body && typeof body === 'object' && body.message) {
      return body.message;
    }
    if (typeof err.error === 'string' && err.error.trim()) {
      return err.error;
    }
  }
  return 'Đã xảy ra lỗi không xác định!';
}
