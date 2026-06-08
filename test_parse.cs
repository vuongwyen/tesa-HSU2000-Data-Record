using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        string filePath = "25063_88103-00031-00_5060030-01_Roll 6_t.txt";
        var forceValues = new List<decimal>();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var sanitizedLine = line.Replace(',', '.');
            var columns = sanitizedLine.Split(';');
            string targetColumn = columns.Length > 1 ? columns[1] : columns[0];
            if (decimal.TryParse(targetColumn.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal force))
            {
                forceValues.Add(force);
            }
        }
        Console.WriteLine("Count: " + forceValues.Count);
        if (forceValues.Count > 0) Console.WriteLine("Avg: " + forceValues.Average());
    }
}
