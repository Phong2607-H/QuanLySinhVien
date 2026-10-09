import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { ToastService } from './toast';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = 'https://localhost:7280/api/XacThuc';
  private isBrowser: boolean;

  constructor(
    private http: HttpClient,
    @Inject(PLATFORM_ID) platformId: object,
    private toastService: ToastService
  ) {
    this.isBrowser = isPlatformBrowser(platformId);
  }

  register(userData: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/dangky`, userData);
  }

  login(credentials: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/dangnhap`, credentials).pipe(
      tap(res => {
        if (res && res.token && this.isBrowser) {
          localStorage.setItem('token', res.token);
          localStorage.setItem('fullName', res.fullName);
          localStorage.setItem('role', res.role);
        }
      })
    );
  }

  logout(): void {
    if (this.isBrowser) {
      localStorage.removeItem('token');
      localStorage.removeItem('fullName');
      localStorage.removeItem('role');
    }
  }

  isLoggedIn(): boolean {
    if (this.isBrowser) {
      return !!localStorage.getItem('token');
    }
    return false;
  }

  getUserName(): string {
    if (this.isBrowser) {
      return localStorage.getItem('fullName') || '';
    }
    return '';
  }
  getRole(): string {
    if (!this.isBrowser) return '';
    const token = localStorage.getItem('token');
    if (!token) return '';

    try {
      // 1. Giải mã Payload từ JWT Token thật
      const payloadBase64 = token.split('.')[1];
      const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(decodeURIComponent(escape(decodedJson)));

      const tokenRole = payload['role'] ||
        payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
        '';

      // 2. Chống giả mạo: nếu role ở localStorage bị cố tình đổi thành Admin
      const localRole = localStorage.getItem('role');
      if (localRole && tokenRole && localRole !== tokenRole) {
        localStorage.setItem('role', tokenRole); // Trả lại role thật
        if (this.isBrowser) {
          this.toastService.showError('Không thể truy cập dưới quyền Admin.');
        }
      }

      return tokenRole;
    } catch {
      return localStorage.getItem('role') || '';
    }
  }

  hasRole(roleName: string): boolean {
    return this.getRole() === roleName;
  }
}
