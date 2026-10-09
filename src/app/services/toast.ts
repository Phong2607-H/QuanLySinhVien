import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface ToastMessage {
  message: string;
  type: 'success' | 'error' | 'warning';
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  toastState = new Subject<ToastMessage>();

  showSuccess(msg: string) {
    this.emit({ message: msg, type: 'success' });
  }

  showError(msg: string) {
    this.emit({ message: msg, type: 'error' });
  }

  showWarning(msg: string) {
    this.emit({ message: msg, type: 'warning' });
  }

  private emit(toast: ToastMessage) {
    // 1. Nếu ToastComponent đang mở và lắng nghe, hiển thị trực tiếp và KHÔNG lưu sessionStorage
    if (this.toastState.observed) {
      this.toastState.next(toast);
      if (typeof window !== 'undefined') {
        sessionStorage.removeItem('flashToast');
      }
    }
    // 2. Nếu không có component nào hứng (ví dụ trước khi reload hoặc redirect trang), mới lưu vào sessionStorage
    else if (typeof window !== 'undefined') {
      try {
        sessionStorage.setItem('flashToast', JSON.stringify(toast));
      } catch { }
    }
  }
}
