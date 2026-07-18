using System.Collections.Generic;

namespace YtypGrouper;

/// <summary>
/// Result of a sort operation: output path, entity counts per source YTYP,
/// and unresolved count (archetype not found in GameFileCache).
/// </summary>
public class SortResult
{
    public string OutputPath { get; set; } = "";
    public int TotalSorted { get; set; }
    public int TotalUnresolved { get; set; }
    public List<(string YtypName, int Count)> Groups { get; set; } = [];
}
