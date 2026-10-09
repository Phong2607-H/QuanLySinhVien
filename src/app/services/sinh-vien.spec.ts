import { TestBed } from '@angular/core/testing';

import { SinhVien } from './sinh-vien';

describe('SinhVien', () => {
  let service: SinhVien;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(SinhVien);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
