namespace tesa_HSU2000_Data_Record.Models;

public class AppSetting
{
    public int Id { get; set; }
    
    // Đường dẫn thư mục cần giám sát
    public string WatchFolderPath { get; set; } = string.Empty;
}
