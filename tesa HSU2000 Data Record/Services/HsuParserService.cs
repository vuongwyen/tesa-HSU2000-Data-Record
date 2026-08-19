using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using tesa_HSU2000_Data_Record.Models;

namespace tesa_HSU2000_Data_Record.Services;

public class HsuParserService
{
    /// <summary>
    /// Parses a given HSU-2000 text file to extract the force values and calculate the average.
    /// Does not extract metadata (Nart, Batch, etc.) as those are provided via UI.
    /// </summary>
    public async Task<HsuTestResult> ParseFileAsync(string filePath)
    {
        var result = new HsuTestResult
        {
            TestedAt = File.GetCreationTime(filePath),
            // Timestamp and other metadata will be populated by the UI
        };

        var forceValues = new List<decimal>();

        // Safe file reading with FileShare.ReadWrite to prevent lock conflicts
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Handle commas replacing with dots for standard invariant parsing
            var sanitizedLine = line.Replace(',', '.');
            
            // Semicolon separated, find the force column
            var columns = sanitizedLine.Split(';');
            
            // Assume the force value is in the second column or fallback to first
            string targetColumn = columns.Length > 1 ? columns[1] : columns[0];

            if (decimal.TryParse(targetColumn.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal force))
            {
                forceValues.Add(force);
                
                // Cố gắng lấy độ dài từ cột 0
                if (columns.Length > 1 && decimal.TryParse(columns[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal length))
                {
                    result.MaxLength = Math.Max(result.MaxLength, length);
                }
            }
        }

        if (forceValues.Any())
        {
            result.AvgValue = forceValues.Average();
            result.RawForceData = forceValues;
        }

        return result;
    }
}
