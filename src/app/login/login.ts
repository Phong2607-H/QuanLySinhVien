import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth';
import { layThongBaoLoi } from '../utils/error-message';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class LoginComponent {
  credentials = {
    username: '',
    password: ''
  };
  errorMessage: string = '';

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
    this.authService.login(this.credentials).subscribe({
      next: () => {
        this.router.navigate(['/sinh-vien']);
      },
      error: (err) => {
        console.error('Login error:', err);
        this.errorMessage = layThongBaoLoi(err);
        this.cdr.detectChanges(); // Cập nhật giao diện ngay (ứng dụng chạy zoneless)
      }
    });
  }

  goToRegister(): void {
    this.router.navigate(['/register']);
  }
}
