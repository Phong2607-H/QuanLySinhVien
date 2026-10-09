import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuditLogService } from '../services/audit-log';
import { layThongBaoLoi } from '../utils/error-message';

@Component({
  selector: 'app-lich-su',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './lich-su.html',
  styleUrls: ['./lich-su.css']
})
export class LichSuComponent implements OnInit {
  logs: any[] = [];
  errorMessage: string = '';

  constructor(
    private auditLogService: AuditLogService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    if (typeof window !== 'undefined') {
      this.taiLichSu();
    }
  }

  taiLichSu(): void {
    this.auditLogService.getLogs().subscribe({
      next: (data) => {
        this.logs = data;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.errorMessage = layThongBaoLoi(err);
        this.cdr.detectChanges();
      }
    });
  }

}
