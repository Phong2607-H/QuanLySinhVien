import { Component, OnInit, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpEventType } from '@angular/common/http';
import { Router, RouterModule } from '@angular/router';
import { SinhVienService } from '../services/sinh-vien';
import { SinhVien } from '../models/sinh-vien';
import { AuthService } from '../services/auth';
import {ToastService} from "../services/toast";


@Component({
  selector: 'app-sinh-vien',
  standalone: true,

  imports: [
    CommonModule,
    FormsModule,
    RouterModule
  ],

  templateUrl: './sinh-vien.html',
  styleUrl: './sinh-vien.css'
})
export class SinhVienComponent implements OnInit {

  // API C#
  apiUrl = 'https://localhost:7280/api/SinhVien';

  // Danh sách sinh viên
  danhSachSinhVien: SinhVien[] = [];

  // Sinh viên đang nhập trên form
  sinhVien: SinhVien = {
    id: 0,
    hoTen: '',
    email: '',
    tuoi: 0
  };

  // null = thêm
  // có ID = sửa
  dangSuaId: number | null = null;
  errorMessage: string = '';
  trangHienTai: number = 1;
  soDongMoiTrang: number = 5; // Để mặc định 5 bản ghi một trang
  tongSoDong: number = 0;
  tongSoTrang: number = 0;
  tuKhoaTimKiem: string = '';
  sapXepTheo: string = 'Id';
  sapXepGiamDan: boolean = false;
  fileDuocChon: File | null = null;
  tienTrinhUpload: number = 0; // Lưu phần trăm (%) tiến độ tải lên
  dangUploadStudentId: number | null = null; // Lưu ID sinh viên đang upload
  formSelectedFile: File | null = null; // Lưu file ảnh được chọn từ form thêm/sửa
  dangUploadForm: boolean = false; // Trạng thái tải lên của form thêm/sửa
  previewAvatarUrl: string | null = null; // Lưu đường dẫn ảnh xem trước (Preview)

  @ViewChild('fileInputForm') fileInputForm?: ElementRef<HTMLInputElement>;

  chonFileChoForm(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.formSelectedFile = file;

      // Đọc file để tạo ảnh xem trước (Preview) ngay lập tức trên form
      const reader = new FileReader();
      reader.onload = () => {
        this.previewAvatarUrl = reader.result as string;
        this.cdr.detectChanges(); // Cập nhật hiển thị ngay lên giao diện
      };
      reader.readAsDataURL(file);
    }
  }

  constructor(
    private sinhVienService: SinhVienService,
    public authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toastService: ToastService // Angular chỉ đưa service vào component qua constructor (dependency injection). Không khai báo ở đây thì không gọi được
  ) { }

  getUserName(): string {
    return this.authService.getUserName();
  }

  dangXuat(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }


  // ==========================================
  // KHI MỞ TRANG
  // ==========================================

  ngOnInit(): void {
    if (typeof window !== 'undefined') {
      this.taiDanhSach();
    }
  }


  // ==========================================
  // GET - LẤY DANH SÁCH
  // ==========================================

  taiDanhSach(): void {
    this.sinhVienService.getAll(
      this.trangHienTai,
      this.soDongMoiTrang,
      this.tuKhoaTimKiem,
      this.sapXepTheo,
      this.sapXepGiamDan
    )
      .subscribe({
        next: (res) => {
          this.danhSachSinhVien = res.items; // Nhận danh sách từ items
          this.tongSoDong = res.totalCount;
          this.tongSoTrang = res.totalPages;
          this.errorMessage = '';
          this.cdr.detectChanges();
        },
        error: (error) => {
          console.error('Lỗi GET:', error);
          this.errorMessage = 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại backend!';
        }
      });
  }
  chuyenTrang(trang: number): void {
    if (trang < 1 || trang > this.tongSoTrang) return;
    this.trangHienTai = trang;
    this.taiDanhSach();
  }

  timKiem(): void {
    this.trangHienTai = 1; // Reset về trang 1 khi tìm kiếm
    this.taiDanhSach();
  }

  thayDoiSapXep(cot: string): void {
    if (this.sapXepTheo === cot) {
      this.sapXepGiamDan = !this.sapXepGiamDan; // Đảo chiều sắp xếp
    } else {
      this.sapXepTheo = cot;
      this.sapXepGiamDan = false; // Mặc định sắp xếp tăng dần
    }
    this.taiDanhSach();
  }

  // ==========================================
  // THÊM / SỬA
  // ==========================================

  luu(): void {
    this.errorMessage = '';

    // ĐANG SỬA
    if (this.dangSuaId !== null) {
      this.sinhVienService.update(this.sinhVien)
        .subscribe({
          next: () => {
            // Nếu có chọn file ảnh mới trong form, thực hiện upload luôn
            if (this.formSelectedFile) {
              this.dangUploadForm = true;
              this.tienTrinhUpload = 0;
              this.sinhVienService.uploadAvatar(this.dangSuaId!, this.formSelectedFile).subscribe({
                next: (event: any) => {
                  if (event.type === HttpEventType.UploadProgress) { // Tiến trình tải lên (HttpEventType.UploadProgress)
                    if (event.total) {
                      this.tienTrinhUpload = Math.round(100 * event.loaded / event.total);
                      this.cdr.detectChanges();
                    }
                  }
                  else if (event.type === HttpEventType.Response) { // Hoàn tất upload
                    this.dangUploadForm = false;
                    this.formSelectedFile = null;
                    this.lamMoiForm();
                    this.taiDanhSach(); // Tải lại danh sách từ DB để trang user được cập nhật tức thì
                    this.toastService.showSuccess('Cập nhật thông tin và ảnh đại diện thành công!');
                  }
                },
                error: (err) => {
                  console.error('Lỗi upload avatar khi sửa:', err);
                  this.dangUploadForm = false;
                  this.lamMoiForm();
                  this.taiDanhSach();
                  this.toastService.showWarning('Cập nhật thông tin thành công nhưng tải ảnh đại diện mới thất bại!');
                }
              });
            } else {
              this.lamMoiForm();
              this.taiDanhSach(); // Tải lại từ DB để cập nhật trang user
              this.toastService.showSuccess('Cập nhật thành công!');
            }
          },
          error: (error) => {
            console.error('Lỗi sửa:', error);
            const laDungDo = error.status === 409 && error.error?.message?.includes('người khác thay đổi');
            if (laDungDo) {
              this.lamMoiForm();
              this.taiDanhSach();
            }
          }
        });
      return;
    }

    // ĐANG THÊM
    const duLieuThem = {
      id: 0,
      hoTen: this.sinhVien.hoTen,
      email: this.sinhVien.email,
      tuoi: this.sinhVien.tuoi
    };

    this.sinhVienService.create(duLieuThem)
      .subscribe({
        next: (data) => {
          console.log('Sinh viên vừa thêm:', data);
          
          // Nếu có chọn file ảnh từ form, tải ảnh lên cho sinh viên vừa tạo
          if (this.formSelectedFile && data.id) {
            this.dangUploadForm = true;
            this.tienTrinhUpload = 0;
            this.sinhVienService.uploadAvatar(data.id, this.formSelectedFile).subscribe({
              next: (event: any) => {
                if (event.type === HttpEventType.UploadProgress) { // Tiến trình tải lên (HttpEventType.UploadProgress)
                  if (event.total) {
                    this.tienTrinhUpload = Math.round(100 * event.loaded / event.total);
                    this.cdr.detectChanges();
                  }
                }
                else if (event.type === HttpEventType.Response) { // Hoàn tất upload
                  this.dangUploadForm = false;
                  this.formSelectedFile = null;
                  this.lamMoiForm();
                  this.taiDanhSach(); // Tải lại danh sách từ DB
                  this.toastService.showSuccess('Thêm sinh viên và tải ảnh đại diện thành công!');
                }
              },
              error: (err) => {
                console.error('Lỗi upload avatar khi thêm:', err);
                this.dangUploadForm = false;
                this.lamMoiForm();
                this.taiDanhSach();
                this.toastService.showWarning('Thêm sinh viên thành công nhưng tải ảnh đại diện thất bại!');
              }
            });
          } else {
            this.lamMoiForm();
            this.taiDanhSach(); // Tải lại danh sách từ DB
            this.toastService.showSuccess('Thêm sinh viên thành công!');
          }
        },
        error: (error) => {
          console.error('Lỗi thêm:', error);
          
        }
      });
  }


  // ==========================================
  // SỬA
  // ==========================================

  sua(sv: SinhVien): void {
    this.dangSuaId = sv.id;
    this.formSelectedFile = null; // Reset file upload khi bắt đầu sửa
    this.previewAvatarUrl = null; // Reset ảnh xem trước tạm thời
    if (this.fileInputForm?.nativeElement) {
      this.fileInputForm.nativeElement.value = '';
    }
    if (typeof document !== 'undefined') {
      const inputEl = document.getElementById('formFileInput') as HTMLInputElement;
      if (inputEl) inputEl.value = '';
    }
    this.sinhVien = {
      id: sv.id,
      hoTen: sv.hoTen,
      email: sv.email,
      tuoi: sv.tuoi,
      avatarUrl: sv.avatarUrl,
      rowVersion: sv.rowVersion // 👈 BẮT BUỘC: Giữ nguyên ảnh đại diện hiện tại để tránh bị mất trên UI
    };
  }


  // ==========================================
  // XÓA
  // ==========================================

  xoa(id: number): void {
    if (!confirm('Bạn có chắc muốn xóa sinh viên này?')) {
      return;
    }

    this.errorMessage = '';
    this.sinhVienService.delete(id)
      .subscribe({
        next: () => {
          if (this.dangSuaId === id) this.lamMoiForm();
          // Bỏ dòng xóa thủ công này:
          // this.danhSachSinhVien = this.danhSachSinhVien.filter(sv => sv.id !== id);
          if (this.danhSachSinhVien.length === 1 && this.trangHienTai > 1) {
            this.trangHienTai--;
          }

          this.taiDanhSach(); // 👈 THÊM DÒNG NÀY: Tải lại từ server để dồn hàng trang sau lên trang trước

          this.toastService.showSuccess('Xóa thành công!');
        },
        error: (error) => {
          console.error('Lỗi xóa:', error);
          
        }
      });
  }


  // ==========================================
  // LÀM TRỐNG FORM
  // ==========================================

  lamMoiForm(): void {
    this.sinhVien = {
      id: 0,
      hoTen: '',
      email: '',
      tuoi: 0
    };
    this.dangSuaId = null;
    this.formSelectedFile = null; // Reset file đã chọn trong form
    this.previewAvatarUrl = null; // Reset ảnh xem trước

    // Reset sạch thẻ <input type="file"> để không còn lưu tên file cũ
    if (this.fileInputForm?.nativeElement) {
      this.fileInputForm.nativeElement.value = '';
    }
    if (typeof document !== 'undefined') {
      const inputEl = document.getElementById('formFileInput') as HTMLInputElement;
      if (inputEl) {
        inputEl.value = '';
      }
    }
  }
  taiLenAvatarTuDong(id: number, event: any): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.dangUploadStudentId = id;
    this.tienTrinhUpload = 0;

    this.sinhVienService.uploadAvatar(id, file).subscribe({
      next: (event: any) => {
        // Sự kiện tiến trình upload (type = 1 nghĩa là HttpEventType.UploadProgress)
        if (event.type === HttpEventType.UploadProgress) {
          if (event.total) {
            this.tienTrinhUpload = Math.round(100 * event.loaded / event.total); // Tính phần trăm (%)
            this.cdr.detectChanges(); // Cập nhật giao diện hiển thị ngay
          }
        }
        // Sự kiện hoàn tất response (type = 4 nghĩa là HttpEventType.Response)
        else if (event.type === HttpEventType.Response) {
          this.toastService.showSuccess('Tải ảnh đại diện lên thành công!');
          this.dangUploadStudentId = null;
          this.taiDanhSach(); // Gọi lại hàm để cập nhật hiển thị ảnh đại diện mới trên bảng
        }
      },
      error: (err) => {
        console.error('Lỗi upload:', err);
        this.dangUploadStudentId = null;
        this.cdr.detectChanges();
      }
    });
  }
}
