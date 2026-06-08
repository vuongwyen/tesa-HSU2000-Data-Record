using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using tesa_HSU2000_Data_Record.Models;

namespace tesa_HSU2000_Data_Record.Services;

public class NetworkSyncService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<HsuTestResult> _localBuffer;
    private readonly string _apiUrl;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _backgroundTask;

    public event EventHandler<string>? SyncStatusChanged;

    public NetworkSyncService(HttpClient httpClient, string apiUrl, string jwtToken)
    {
        _httpClient = httpClient;
        _apiUrl = apiUrl.TrimEnd('/') + "/api/scale/sync";
        _localBuffer = new ConcurrentQueue<HsuTestResult>();
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
    }

    public void EnqueueForSync(HsuTestResult result)
    {
        _localBuffer.Enqueue(result);
        SyncStatusChanged?.Invoke(this, $"Đã đợi: {result.Nart} (Chờ: {_localBuffer.Count})");
    }

    public void StartSyncing()
    {
        if (_cancellationTokenSource != null) return;

        _cancellationTokenSource = new CancellationTokenSource();
        _backgroundTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
    }

    public void StopSyncing()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = null;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_localBuffer.TryPeek(out var result))
            {
                try
                {
                    // Attempt to send data via POST
                    var response = await _httpClient.PostAsJsonAsync(_apiUrl, result, cancellationToken);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        // Successfully sent, remove from queue
                        _localBuffer.TryDequeue(out _);
                        SyncStatusChanged?.Invoke(this, $"Thành công: {result.Nart} (Chờ: {_localBuffer.Count})");
                    }
                    else
                    {
                        SyncStatusChanged?.Invoke(this, $"Lỗi Máy Chủ: {response.StatusCode} - Đang thử lại...");
                        await Task.Delay(5000, cancellationToken); // Wait before retry
                    }
                }
                catch (Exception ex)
                {
                    // Connection offline or other network error
                    SyncStatusChanged?.Invoke(this, $"Mất Kết Nối: {ex.Message} - Đã lưu tạm");
                    await Task.Delay(5000, cancellationToken); // Network error, wait and retry
                }
            }
            else
            {
                // Queue is empty, wait before checking again
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        StopSyncing();
        _httpClient.Dispose();
    }
}
