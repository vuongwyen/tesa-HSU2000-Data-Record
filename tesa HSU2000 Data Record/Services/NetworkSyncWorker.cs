using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using tesa_HSU2000_Data_Record.Data;
using tesa_HSU2000_Data_Record.Models;

namespace tesa_HSU2000_Data_Record.Services;

public class NetworkSyncWorker : IDisposable
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private string _currentIdempotencyKey = string.Empty;
    private string _currentBatchHash = string.Empty;
    
    
    public event EventHandler<string>? SyncStatusChanged;

    public NetworkSyncWorker()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public void Start()
    {
        if (_cts != null) return; // already running
        
        _cts = new CancellationTokenSource();
        _backgroundTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (_cts == null) return;
        
        _cts.Cancel();
        try
        {
            _backgroundTask?.Wait();
        }
        catch { /* ignored */ }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task WorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ProcessSyncAsync(token);
                await Task.Delay(5000, token); // Check every 5 seconds
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SyncStatusChanged?.Invoke(this, $"Lỗi đồng bộ: {ex.Message}");
                await Task.Delay(10000, token); // Wait longer on error
            }
        }
    }

    private async Task ProcessSyncAsync(CancellationToken token)
    {
        // 1. Get Settings & check if configured
        AppSetting? settings;
        List<HsuTestResult> pendingRecords;

        using (var db = new AppDbContext())
        {
            settings = db.Settings.FirstOrDefault();
            if (settings == null || string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            {
                SyncStatusChanged?.Invoke(this, "Đang chờ: Chưa cấu hình Server API");
                return;
            }

            // 2. Fetch pending records
            pendingRecords = db.TestResults.Where(x => !x.IsSynced).OrderBy(x => x.Id).Take(50).ToList();
        }

        if (!pendingRecords.Any())
        {
            SyncStatusChanged?.Invoke(this, "Đồng bộ: Đã cập nhật tất cả.");
            return;
        }



        // 4. Send Data
        SyncStatusChanged?.Invoke(this, $"Đang đồng bộ {pendingRecords.Count} bản ghi...");
        
        var payloadData = pendingRecords.Select(r => new
        {
            recordId = r.RecordId,
            appId = "tesa-HSU2000-WinForms",
            appType = "Scale",
            testedAt = r.TestedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            payload = new
            {
                WeightValue = r.AvgValue,
                Unit = r.Unit,
                BatchCode = r.BatchCode,
                Location = r.Location,
                SampleName = r.SampleName,
                Nart = r.Nart,
                Tester = r.Tester
            }
        }).ToList();

        var requestBody = new
        {
            deviceId = settings.DeviceId,
            data = payloadData
        };

        var batchIds = pendingRecords.Select(p => p.Id).OrderBy(id => id);
        string newBatchHash = string.Join(",", batchIds);

        if (_currentBatchHash != newBatchHash || string.IsNullOrEmpty(_currentIdempotencyKey))
        {
            _currentIdempotencyKey = Guid.NewGuid().ToString();
            _currentBatchHash = newBatchHash;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.ApiBaseUrl.TrimEnd('/')}/api/scale/sync");
        request.Headers.Add("X-Api-Key", settings.ApiPassword); // Using ApiPassword field to store ApiKey
        request.Headers.Add("Idempotency-Key", _currentIdempotencyKey);
        request.Content = JsonContent.Create(requestBody);

        var response = await _httpClient.SendAsync(request, token);
        
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Success or already exists, mark as synced
            using (var db = new AppDbContext())
            {
                var ids = pendingRecords.Select(p => p.Id).ToList();
                var recordsToUpdate = db.TestResults.Where(r => ids.Contains(r.Id)).ToList();
                foreach (var r in recordsToUpdate)
                {
                    r.IsSynced = true;
                }
                await db.SaveChangesAsync(token);
            }
            SyncStatusChanged?.Invoke(this, $"Đã đồng bộ {pendingRecords.Count} bản ghi.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            SyncStatusChanged?.Invoke(this, "Thiết bị đã bị Block trên Server!");
            // Sleep longer to prevent spamming
            await Task.Delay(30000, token); 
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            SyncStatusChanged?.Invoke(this, "Gửi quá nhanh, Rate Limit 429.");
            await Task.Delay(2000, token);
        }
        else
        {
            SyncStatusChanged?.Invoke(this, $"Lỗi server: {(int)response.StatusCode}");
        }
    }



    public void Dispose()
    {
        Stop();
        _httpClient.Dispose();
    }
}
