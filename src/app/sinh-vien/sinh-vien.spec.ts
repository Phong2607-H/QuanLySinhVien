import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SinhVien } from './sinh-vien';

describe('SinhVien', () => {
  let component: SinhVien;
  let fixture: ComponentFixture<SinhVien>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SinhVien],
    }).compileComponents();

    fixture = TestBed.createComponent(SinhVien);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
