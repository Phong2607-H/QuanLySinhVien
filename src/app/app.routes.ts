import { Routes } from '@angular/router';
import { LoginComponent } from './login/login';
import { RegisterComponent } from './register/register';
import { SinhVienComponent } from './sinh-vien/sinh-vien';
import { LichSuComponent } from './lich-su/lich-su';
import { roleGuard } from './guards/role';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from './services/auth';

const authGuard = () => {
  const platformId = inject(PLATFORM_ID);
  if (!isPlatformBrowser(platformId)) {
    return true;
  }
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isLoggedIn()) {
    return true;
  }
  return router.parseUrl('/login');
};

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'sinh-vien', component: SinhVienComponent, canActivate: [authGuard] },
  { path: 'lich-su', component: LichSuComponent, canActivate: [roleGuard(['Admin'])] },
  { path: '', redirectTo: '/sinh-vien', pathMatch: 'full' },
  { path: '**', redirectTo: '/sinh-vien' }
];
