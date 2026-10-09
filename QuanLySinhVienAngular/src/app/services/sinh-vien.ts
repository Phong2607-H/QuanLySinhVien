import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SinhVien } from '../models/sinh-vien';

@Injectable({
  providedIn: 'root'
})
export class SinhVienService {

  private apiUrl = 'https://localhost:7280/api/SinhVien';

  constructor(private http: HttpClient) { }

  getAll(page: number, size: number, keyword: string, sortBy: string, isDesc: boolean): Observable<any> {
    let params = `?pageNumber=${page}&pageSize=${size}`;
    if (keyword) params += `&keyword=${encodeURIComponent(keyword)}`;
    if (sortBy) params += `&sortBy=${sortBy}&isDescending=${isDesc}`;
    return this.http.get<any>(`${this.apiUrl}${params}`);
  }

  create(sinhVien: SinhVien): Observable<SinhVien> {
    return this.http.post<SinhVien>(this.apiUrl, sinhVien);
  }

  update(sinhVien: SinhVien): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/${sinhVien.id}`,
      sinhVien
    );
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }
  uploadAvatar(id: number, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file); // Đóng gói file gửi dạng Multipart Form Data

    return this.http.post(`${this.apiUrl}/upload-avatar/${id}`, formData, {
      reportProgress: true, // 👈 Bắt buộc: Kích hoạt lắng nghe tiến trình upload
      observe: 'events'     // 👈 Bắt buộc: Lấy toàn bộ HTTP events để tính toán (%)
    });
  }
}
