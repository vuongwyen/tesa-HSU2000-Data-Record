using System;
using System.IO;
using System.Threading.Tasks;
using tesa_HSU2000_Data_Record.Models;

namespace tesa_HSU2000_Data_Record.Services;

public class HsuWatcherManager : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly HsuParserService _parserService;
    private readonly string _monitorDirectory;
    private readonly string _processedDirectory;

    public event EventHandler<HsuTestResult>? FileProcessed;
    public event EventHandler<string>? ErrorOccurred;

    public HsuWatcherManager(string monitorDirectory, HsuParserService parserService)
    {
        _monitorDirectory = monitorDirectory;
        _parserService = parserService;
        _processedDirectory = Path.Combine(_monitorDirectory, "Processed");

        if (!Directory.Exists(_monitorDirectory))
        {
            Directory.CreateDirectory(_monitorDirectory);
        }

        if (!Directory.Exists(_processedDirectory))
        {
            Directory.CreateDirectory(_processedDirectory);
        }

        _watcher = new FileSystemWatcher(_monitorDirectory, "*.txt")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = false
        };

        _watcher.Created += OnFileCreated;
    }

    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Give the manufacturer's software time to completely flush the buffer to the disk
            await Task.Delay(500);

            var result = await _parserService.ParseFileAsync(e.FullPath);
            
            // Move file to Processed sub-folder
            var destinationPath = Path.Combine(_processedDirectory, e.Name!);
            
            // Handle duplicate files in processed folder if any exist
            if (File.Exists(destinationPath))
            {
                destinationPath = Path.Combine(_processedDirectory, $"{Path.GetFileNameWithoutExtension(e.Name)}_{DateTime.Now.Ticks}.txt");
            }
            
            File.Move(e.FullPath, destinationPath);

            // Notify UI or other services
            FileProcessed?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error processing file {e.Name}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.Created -= OnFileCreated;
            _watcher.Dispose();
        }
    }
}
