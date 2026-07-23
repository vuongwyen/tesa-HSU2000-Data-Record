namespace tesa_HSU2000_Data_Record.Models;

public class AppSetting
{
    public int Id { get; set; }
    
    // Đường dẫn thư mục cần giám sát
    public string WatchFolderPath { get; set; } = string.Empty;

    // Cấu hình Server API
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ApiUsername { get; set; } = string.Empty;
    public string ApiPassword { get; set; } = string.Empty;
    public string DeviceId { get; set; } = System.Environment.MachineName;
}
