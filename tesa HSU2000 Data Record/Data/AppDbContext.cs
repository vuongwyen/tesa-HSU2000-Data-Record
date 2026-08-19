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

    public void MigrateSchema()
    {
        // Add RecordId and IsSynced to TestResults if they don't exist
        try
        {
            this.Database.ExecuteSqlRaw("ALTER TABLE TestResults ADD COLUMN RecordId TEXT NOT NULL DEFAULT '';");
            this.Database.ExecuteSqlRaw("UPDATE TestResults SET RecordId = lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))) WHERE RecordId = '';");
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("RecordId: " + ex.Message); }
        
        try
        {
            this.Database.ExecuteSqlRaw("ALTER TABLE TestResults ADD COLUMN IsSynced INTEGER NOT NULL DEFAULT 0;");
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("IsSynced: " + ex.Message); }

        try
        {
            this.Database.ExecuteSqlRaw("ALTER TABLE TestResults ADD COLUMN MaxLength TEXT NOT NULL DEFAULT '0';");
        }
        catch (System.Exception ex) 
        { 
            if (!ex.Message.Contains("duplicate column name"))
            {
                System.Windows.Forms.MessageBox.Show("Lỗi nâng cấp Database (MaxLength): " + ex.Message); 
            }
        }
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
