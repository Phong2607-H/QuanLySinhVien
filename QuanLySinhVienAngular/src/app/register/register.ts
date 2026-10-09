import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth';
import { layThongBaoLoi } from '../utils/error-message';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './register.html',
  styleUrl: './register.css'
})
export class RegisterComponent {
  user = {
    username: '',
    password: '',
    fullName: ''
  };
  errorMessage: string = '';
  successMessage: string = '';

  constructor(
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/sinh-vien']);
    }
  }

  onSubmit(): void {
    this.errorMessage = '';
    this.successMessage = '';

    this.authService.register(this.user).subscribe({
      next: () => {
        this.successMessage = 'Đăng ký thành công! Đang chuyển hướng đăng nhập...';
        this.cdr.detectChanges(); // Cập nhật giao diện ngay
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 1500);
      },
      error: (err) => {
        console.error('Register error:', err);
        this.errorMessage = layThongBaoLoi(err);
        this.cdr.detectChanges(); // Cập nhật giao diện ngay (ứng dụng chạy zoneless)
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
