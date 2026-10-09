import { HttpInterceptorFn } from '@angular/common/http';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  // Kiểm tra môi trường chạy có phải là trình duyệt không (tránh lỗi SSR/Prerender)
  if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
    const token = localStorage.getItem('token');
    if (token) {
      const clonedReq = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });
      return next(clonedReq);
    }
  }
  return next(req);
};
