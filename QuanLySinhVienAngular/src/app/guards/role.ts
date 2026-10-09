import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth';
import { ToastService } from '../services/toast';

export const roleGuard = (allowedRoles: string[]): CanActivateFn => {
  return () => {
    const platformId = inject(PLATFORM_ID);
    // Nếu đang chạy trên Server (SSR), bỏ qua để Client (trình duyệt) kiểm tra localStorage
    if (!isPlatformBrowser(platformId)) {
      return true;
    }

    const authService = inject(AuthService);
    const router = inject(Router);
    const toastService = inject(ToastService);
    const userRole = authService.getRole();

    // Nếu đã đăng nhập và quyền thực sự thuộc danh sách cho phép
    if (!authService.isLoggedIn()) {
      return router.parseUrl('/login');
    }
    if (allowedRoles.includes(authService.getRole())) {
      return true;
    }
    // Nếu không phải Admin:
    toastService.showError('Không thể truy cập dưới quyền Admin.');
    // Trả về UrlTree để Angular điều hướng chuẩn xác về /sinh-vien
    return router.parseUrl('/sinh-vien');
  };
};
