import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth';
import { ToastService } from '../services/toast';
import { ApiError } from '../models/api-error';
import { layThongBaoLoi } from '../utils/error-message';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Đang render phía server (SSR) thì không hiện toast, không điều hướng
      if (typeof window === 'undefined') return throwError(() => error);

      // Chỉ có ở môi trường Development: in chi tiết lỗi ra Console để debug
      const apiError = error.error as ApiError | null;
      if (apiError?.details) console.error('[API details]', apiError.details);

      // Trang Đăng nhập/Đăng ký tự hiện banner lỗi → không toast để tránh báo 2 lần
      const laApiXacThuc = req.url.includes('/api/XacThuc/');
      if (!laApiXacThuc) toastService.showError(layThongBaoLoi(error));

      if (error.status === 401 && !laApiXacThuc) {
        authService.logout();
        router.navigate(['/login']);
      } else if (error.status === 403) {
        router.navigate(['/sinh-vien']);
      }

      return throwError(() => error);
    })
  );
};
