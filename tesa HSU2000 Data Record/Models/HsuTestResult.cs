using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace tesa_HSU2000_Data_Record.Models;

public class HsuTestResult
{
    // Cột mới làm khóa chính cho Database
    public int Id { get; set; }

    public string Nart { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    public string Tester { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    
    public decimal AvgValue { get; set; }
    public string Unit { get; set; } = "Newton";
    
    public DateTime TestedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public List<decimal> RawForceData { get; set; } = new List<decimal>();
}
