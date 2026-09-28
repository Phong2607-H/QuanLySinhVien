using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using QuanLySinhVien.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLySinhVien.Data
{
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context != null)
            {
                await OnBeforeSaveChanges(context);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private async Task OnBeforeSaveChanges(DbContext context)
        {
            context.ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();

            var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";

            // Dùng .ToList() để quét danh sách an toàn
            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    TableName = entry.Metadata.GetTableName() ?? "Unknown",
                    Username = username
                };

                auditEntries.Add(auditEntry);

                // 1. KIỂM TRA HÀNH ĐỘNG (XÓA MỀM):
                // Nếu bản ghi có cột IsDeleted và giá trị hiện tại là true -> chắc chắn là Xóa
                var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
                bool isSoftDelete = isDeletedProp != null &&
                                    (Equals(isDeletedProp.CurrentValue, true) ||
                                     isDeletedProp.CurrentValue?.ToString()?.ToLower() == "true" ||
                                     isDeletedProp.CurrentValue?.ToString() == "1");

                if (isSoftDelete)
                {
                    auditEntry.Action = "Xóa"; // 👈 Ghi nhận là Xóa
                }
                else if (entry.State == EntityState.Added)
                {
                    auditEntry.Action = "Thêm";
                }
                else if (entry.State == EntityState.Deleted)
                {
                    auditEntry.Action = "Xóa";
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditEntry.Action = "Sửa";
                }

                // 2. GHI NHẬN DỮ LIỆU CŨ VÀ MỚI:
                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue ?? "";
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue ?? "";
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue ?? "";
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue ?? "";
                                auditEntry.NewValues[propertyName] = property.CurrentValue ?? "";
                            }
                            break;
                    }
                }
            } 
            foreach (var auditEntry in auditEntries)
            {
                context.Set<AuditLog>().Add(auditEntry.ToAudit());
            }
        }

        public class AuditEntry
        {
            public AuditEntry(EntityEntry entry)
            {
                Entry = entry;
            }

            public EntityEntry Entry { get; }
            public string Username { get; set; } = "Anonymous";
            public string TableName { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public Dictionary<string, object> KeyValues { get; } = new();
            public Dictionary<string, object> OldValues { get; } = new();
            public Dictionary<string, object> NewValues { get; } = new();

            public AuditLog ToAudit()
            {
                return new AuditLog
                {
                    Username = Username,
                    Action = Action,
                    TableName = TableName,
                    Timestamp = DateTime.Now,
                    OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
                    NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues)
                };
            }
        }
    }
}