using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YtypGrouper;

public class XmlStructureException : Exception
{
    public XmlStructureException(string message) : base(message) { }
}

public class XmlBinaryFormatException : Exception
{
    public XmlBinaryFormatException(string message) : base(message) { }
}

/// <summary>
/// Processes .xml / .ymap / .ytyp files: sorts entities by source YTYP,
/// then writes the sorted result to a user-chosen output directory.
/// </summary>
public class FileProcessor
{
    private readonly EntitySorter _sorter;

    public FileProcessor(GameFileCache cache) => _sorter = new EntitySorter(cache);

    // -------------------------------------------------------------------------
    // Entry point — outputDir is chosen by the user at runtime
    // -------------------------------------------------------------------------

    public SortResult ProcessFile(string filePath, string outputDir)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xml"  => ProcessXml(filePath, outputDir),
            ".ymap" => ProcessBinaryYmap(filePath, outputDir),
            ".ytyp" => ProcessBinaryYtyp(filePath, outputDir),
            _       => throw new XmlStructureException($"Unsupported file type: {ext}")
        };
    }

    // -------------------------------------------------------------------------
    // XML path (.xml CodeWalker export)
    // -------------------------------------------------------------------------

    private SortResult ProcessXml(string filePath, string outputDir)
    {
        var header = ReadHeader(filePath, 50);
        if (!header.Contains("<?xml") && !header.Contains("<Item") &&
            !header.Contains("<CMapData") && !header.Contains("<CMapTypes"))
            throw new XmlBinaryFormatException(
                "This file appears to be a binary game format.\nExport it as XML via CodeWalker first.");

        var doc  = XDocument.Load(filePath, LoadOptions.None);
        var root = doc.Root ?? throw new XmlStructureException("Empty or invalid XML file.");
        var result = new SortResult();

        if (root.Name.LocalName == "CMapData")
        {
            var entities = root.Element("entities")
                ?? throw new XmlStructureException("No <entities> block found in this CMapData.");
            var (count, groups) = _sorter.XmlSortSimpleEntities(entities);
            result.TotalSorted = count;
            result.Groups = groups;
        }
        else if (root.Name.LocalName == "CMapTypes")
        {
            var archetypes = root.Element("archetypes")
                ?? throw new XmlStructureException("No <archetypes> block found in this CMapTypes.");
            var ytypFileName = Path.GetFileNameWithoutExtension(
                                   Path.GetFileNameWithoutExtension(filePath));
            foreach (var mlo in archetypes.Elements("Item")
                         .Where(i => (string?)i.Attribute("type") == "CMloArchetypeDef"))
            {
                var (count, groups) = _sorter.XmlSortMloEntities(mlo, ytypFileName);
                result.TotalSorted += count;
                MergeGroups(result.Groups, groups);
            }
        }
        else
        {
            var entities = root.Element("entities")
                ?? throw new XmlStructureException(
                    "Unrecognised structure: neither a valid YMAP nor YTYP.");
            var (count, groups) = _sorter.XmlSortSimpleEntities(entities);
            result.TotalSorted = count;
            result.Groups = groups;
        }

        result.TotalUnresolved = result.Groups
            .Where(g => g.YtypName == "(unresolved)").Sum(g => g.Count);

        var outPath = MakeOutputPath(filePath, outputDir);
        var settings = new XmlWriterSettings
        {
            Indent = true, IndentChars = "  ",
            Encoding = new System.Text.UTF8Encoding(false)
        };
        using (var w = XmlWriter.Create(outPath, settings)) doc.Save(w);

        result.OutputPath = outPath;
        return result;
    }

    // -------------------------------------------------------------------------
    // Binary YMAP path
    // -------------------------------------------------------------------------

    private SortResult ProcessBinaryYmap(string filePath, string outputDir)
    {
        var ymap = new YmapFile();
        ymap.Load(File.ReadAllBytes(filePath));

        if (!ymap.Loaded)
            throw new XmlStructureException("Failed to load the YMAP binary file.");

        var result = new SortResult();
        var (count, groups) = _sorter.BinarySortYmap(ymap);
        result.TotalSorted = count;
        result.Groups = groups;
        result.TotalUnresolved = groups.Where(g => g.YtypName == "(unresolved)").Sum(g => g.Count);

        var outPath = MakeOutputPath(filePath, outputDir);
        File.WriteAllBytes(outPath, ymap.Save());

        result.OutputPath = outPath;
        return result;
    }

    // -------------------------------------------------------------------------
    // Binary YTYP path
    // -------------------------------------------------------------------------

    private SortResult ProcessBinaryYtyp(string filePath, string outputDir)
    {
        var ytyp = new YtypFile();
        ytyp.Load(File.ReadAllBytes(filePath));

        if (!ytyp.Loaded)
            throw new XmlStructureException("Failed to load the YTYP binary file.");

        var result = new SortResult();
        var ytypFileName = Path.GetFileNameWithoutExtension(filePath);

        if (ytyp.AllArchetypes != null)
        {
            foreach (var arch in ytyp.AllArchetypes.OfType<MloArchetype>())
            {
                var (count, groups) = _sorter.BinarySortMlo(arch, ytypFileName);
                result.TotalSorted += count;
                MergeGroups(result.Groups, groups);
            }
        }

        result.TotalUnresolved = result.Groups
            .Where(g => g.YtypName == "(unresolved)").Sum(g => g.Count);

        var outPath = MakeOutputPath(filePath, outputDir);
        File.WriteAllBytes(outPath, ytyp.Save());

        result.OutputPath = outPath;
        return result;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string MakeOutputPath(string filePath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        return Path.Combine(outputDir, Path.GetFileName(filePath));
    }

    private static void MergeGroups(
        List<(string YtypName, int Count)> target,
        List<(string YtypName, int Count)> source)
    {
        foreach (var (name, count) in source)
        {
            var idx = target.FindIndex(g => g.YtypName == name);
            if (idx >= 0) target[idx] = (name, target[idx].Count + count);
            else target.Add((name, count));
        }
    }

    private static string ReadHeader(string path, int chars)
    {
        using var reader = new StreamReader(path, System.Text.Encoding.UTF8, true);
        var buf = new char[chars];
        var read = reader.Read(buf, 0, chars);
        return new string(buf, 0, read);
    }
}
