import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ToastService, ToastMessage } from '../services/toast';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast.html',
  styleUrls: ['./toast.css']
})
export class ToastComponent implements OnInit, OnDestroy {
  toasts: ToastMessage[] = [];
  private sub!: Subscription;

  constructor(private toastService: ToastService, private cdr: ChangeDetectorRef) { }

  ngOnInit(): void {
    // 1. Kiểm tra nếu có thông báo tồn đọng từ guard hoặc sau khi reload trang
    if (typeof window !== 'undefined') {
      const saved = sessionStorage.getItem('flashToast');
      if (saved) {
        sessionStorage.removeItem('flashToast');
        try {
          const toast = JSON.parse(saved);
          setTimeout(() => this.addToast(toast), 150);
        } catch {}
      }
    }

    // 2. Lắng nghe thông báo phát sinh realtime
    this.sub = this.toastService.toastState.subscribe((toast) => {
      if (typeof window !== 'undefined') {
        sessionStorage.removeItem('flashToast');
      }
      this.addToast(toast);
    });
  }

  private addToast(toast: ToastMessage): void {
    this.toasts.push(toast);
    this.cdr.detectChanges();

    // Tự động xóa thông báo sau 4 giây
    setTimeout(() => {
      this.toasts = this.toasts.filter(t => t !== toast);
      this.cdr.detectChanges();
    }, 4000);
  }

  ngOnDestroy(): void {
    if (this.sub) this.sub.unsubscribe();
  }
}
