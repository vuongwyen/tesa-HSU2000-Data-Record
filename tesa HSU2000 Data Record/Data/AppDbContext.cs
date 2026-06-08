using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text.Json;
using tesa_HSU2000_Data_Record.Models;

namespace tesa_HSU2000_Data_Record.Data;

public class AppDbContext : DbContext
{
    public DbSet<HsuTestResult> TestResults { get; set; } = null!;
    public DbSet<AppSetting> Settings { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Lưu trữ file DB vào thư mục Local AppData để an toàn, không bị xoá khi Visual Studio Build lại code (F5)
        string appDataFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        string dbDir = System.IO.Path.Combine(appDataFolder, "tesa_HSU2000");
        
        if (!System.IO.Directory.Exists(dbDir))
        {
            System.IO.Directory.CreateDirectory(dbDir);
        }

        string dbPath = System.IO.Path.Combine(dbDir, "tesa_hsu2000.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Thiết lập ValueConverter cho RawForceData vì SQLite không hỗ trợ kiểu List natively
        // Chuyển List<decimal> thành chuỗi JSON để lưu, và ngược lại
        modelBuilder.Entity<HsuTestResult>()
            .Property(e => e.RawForceData)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => string.IsNullOrEmpty(v) ? new List<decimal>() : JsonSerializer.Deserialize<List<decimal>>(v, (JsonSerializerOptions)null)
            );
    }
}
