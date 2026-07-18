using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YtypGrouper;

/// <summary>
/// Sorts entities by their source YTYP file, resolved via GameFileCache.
/// Supports two paths:
///   - XML path  : works on XElement nodes from a CodeWalker XML export.
///   - Binary path: works directly on CodeWalker C# objects (YmapFile / MloArchetype).
/// Unresolved archetypes (missing mod, typo…) are placed last.
/// </summary>
public class EntitySorter
{
    private const string UnresolvedGroup = "(unresolved)";

    private readonly GameFileCache _cache;

    public EntitySorter(GameFileCache cache) => _cache = cache;

    // -------------------------------------------------------------------------
    // Internal resolution helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the source YTYP filename (without extension) for a given hash,
    /// or null if the archetype cannot be found in the GameFileCache.
    /// </summary>
    private string? ResolveYtypName(uint hash)
    {
        if (hash == 0) return null;
        var arch = _cache.GetArchetype(hash);
        if (arch?.Ytyp == null) return null;
        var name = arch.Ytyp.Name;
        return string.IsNullOrEmpty(name)
            ? null
            : name.Replace(".ytyp", "", StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // XML PATH  (input = CodeWalker XML export, .ymap.xml / .ytyp.xml)
    // =========================================================================

    /// <summary>
    /// Sorts a flat &lt;entities&gt; block (classic YMAP).
    /// Groups are ordered alphabetically by source YTYP name.
    /// Within a group, entities are ordered alphabetically by archetypeName.
    /// Unresolved entities go last.
    /// </summary>
    public (int sorted, List<(string YtypName, int Count)> groups)
        XmlSortSimpleEntities(XElement entitiesSection)
    {
        var items = entitiesSection.Elements("Item").ToList();
        var classified = new List<(XElement Item, string ArchName, string Group)>();
        var others = new List<XElement>();

        foreach (var item in items)
        {
            var archName = item.Element("archetypeName")?.Value;
            if (string.IsNullOrEmpty(archName)) { others.Add(item); continue; }

            var hash = JenkHash.GenHash(archName.ToLowerInvariant().Trim());
            classified.Add((item, archName, ResolveYtypName(hash) ?? UnresolvedGroup));
        }

        var sorted = SortClassified(classified);

        entitiesSection.RemoveNodes();
        foreach (var d in sorted) entitiesSection.Add(d.Item);
        foreach (var o in others)  entitiesSection.Add(o);

        return (classified.Count, BuildGroupSummary(sorted.Select(d => d.Group)));
    }

    /// <summary>
    /// Sorts an MLO &lt;entities&gt; block and remaps rooms/portals attachedObjects.
    /// Props belonging to the MLO's own YTYP are labelled "(internal)" in the summary.
    /// </summary>
    public (int sorted, List<(string YtypName, int Count)> groups)
        XmlSortMloEntities(XElement mloArchetype, string currentYtypFileName)
    {
        var entitiesSection = mloArchetype.Element("entities");
        if (entitiesSection == null) return (0, []);

        var items = entitiesSection.Elements("Item").ToList();
        if (items.Count == 0) return (0, []);

        var data = new List<(int OldIdx, XElement Elem, string Group, string ArchName, bool Resolved)>();
        for (var i = 0; i < items.Count; i++)
        {
            var archName = items[i].Element("archetypeName")?.Value;
            if (string.IsNullOrEmpty(archName))
            {
                data.Add((i, items[i], UnresolvedGroup, "~unknown", false));
                continue;
            }
            var hash = JenkHash.GenHash(archName.ToLowerInvariant().Trim());
            var ytyp = ResolveYtypName(hash);
            data.Add(ytyp == null
                ? (i, items[i], UnresolvedGroup, archName, false)
                : (i, items[i], ytyp, archName, true));
        }

        var sorted = data
            .OrderBy(d => d.Group == UnresolvedGroup ? 1 : 0)
            .ThenBy(d => d.Group == UnresolvedGroup ? "" : d.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.ArchName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build old→new index map
        var map = new Dictionary<int, int>();
        for (var i = 0; i < sorted.Count; i++) map[sorted[i].OldIdx] = i;

        // Rewrite entities in new order
        entitiesSection.RemoveNodes();
        foreach (var d in sorted) entitiesSection.Add(d.Elem);

        // Remap attachedObjects in rooms and portals
        XmlRemapAttachedObjects(mloArchetype.Element("rooms"), map);
        XmlRemapAttachedObjects(mloArchetype.Element("portals"), map);

        var groups = BuildGroupSummary(sorted.Select(d => d.Group))
            .Select(g => g.YtypName.Equals(currentYtypFileName, StringComparison.OrdinalIgnoreCase)
                ? ($"{g.YtypName} (internal)", g.Count) : g)
            .ToList();

        return (data.Count(d => d.Resolved), groups);
    }

    private static void XmlRemapAttachedObjects(XElement? section, Dictionary<int, int> map)
    {
        if (section == null) return;
        foreach (var item in section.Elements("Item"))
        {
            var attached = item.Element("attachedObjects");
            if (attached == null || string.IsNullOrWhiteSpace(attached.Value)) continue;

            var newIndices = attached.Value
                .Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => int.TryParse(s, out _))
                .Select(s => int.Parse(s))
                .Where(oldIdx => map.ContainsKey(oldIdx))
                .Select(oldIdx => map[oldIdx])
                .OrderBy(x => x)
                .ToList();

            attached.Value = string.Join(" ", newIndices);
        }
    }

    // =========================================================================
    // BINARY PATH  (input = CodeWalker C# objects loaded from raw .ymap/.ytyp)
    // =========================================================================

    /// <summary>
    /// Sorts entities in a YmapFile (loaded from a raw binary .ymap).
    /// Mutates ymap.AllEntities in place; caller must call ymap.Save() afterwards.
    /// </summary>
    public (int sorted, List<(string YtypName, int Count)> groups)
        BinarySortYmap(YmapFile ymap)
    {
        var entities = ymap.AllEntities;
        if (entities == null || entities.Length == 0) return (0, []);

        var classified = new List<(YmapEntityDef Entity, string ArchName, string Group)>();
        var others = new List<YmapEntityDef>();

        foreach (var ent in entities)
        {
            var hash = ent._CEntityDef.archetypeName.Hash;
            var archName = JenkIndex.TryGetString(hash);
            if (string.IsNullOrEmpty(archName)) archName = hash.ToString("X");

            if (hash == 0) { others.Add(ent); continue; }
            classified.Add((ent, archName, ResolveYtypName(hash) ?? UnresolvedGroup));
        }

        var sorted = classified
            .OrderBy(d => d.Group == UnresolvedGroup ? 1 : 0)
            .ThenBy(d => d.Group == UnresolvedGroup ? "" : d.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.ArchName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Rebuild AllEntities with new ordering (index is implicit position in array)
        var newEntities = sorted.Select(d => d.Entity).Concat(others).ToArray();
        for (var i = 0; i < newEntities.Length; i++) newEntities[i].Index = i;
        ymap.AllEntities = newEntities;

        return (classified.Count, BuildGroupSummary(sorted.Select(d => d.Group)));
    }

    /// <summary>
    /// Sorts entities inside a MloArchetype (from a raw binary .ytyp) and remaps
    /// attachedObjects in rooms and portals.
    /// </summary>
    public (int sorted, List<(string YtypName, int Count)> groups)
        BinarySortMlo(MloArchetype mlo, string currentYtypFileName)
    {
        if (mlo.entities == null || mlo.entities.Length == 0) return (0, []);

        var data = new List<(int OldIdx, MCEntityDef Entity, string Group, string ArchName, bool Resolved)>();
        for (var i = 0; i < mlo.entities.Length; i++)
        {
            var ent = mlo.entities[i];
            var hash = ent.Data.archetypeName.Hash;
            var archName = JenkIndex.TryGetString(hash);
            if (string.IsNullOrEmpty(archName)) archName = hash.ToString("X");

            if (hash == 0)
            {
                data.Add((i, ent, UnresolvedGroup, "~unknown", false));
                continue;
            }
            var ytyp = ResolveYtypName(hash);
            data.Add(ytyp == null
                ? (i, ent, UnresolvedGroup, archName, false)
                : (i, ent, ytyp, archName, true));
        }

        var sorted = data
            .OrderBy(d => d.Group == UnresolvedGroup ? 1 : 0)
            .ThenBy(d => d.Group == UnresolvedGroup ? "" : d.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.ArchName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build old→new index map
        var map = new Dictionary<int, int>();
        for (var i = 0; i < sorted.Count; i++) map[sorted[i].OldIdx] = i;

        // Rebuild entities array
        mlo.entities = sorted.Select(d => d.Entity).ToArray();

        // Remap attachedObjects in rooms
        if (mlo.rooms != null)
        {
            foreach (var room in mlo.rooms)
            {
                if (room.AttachedObjects == null) continue;
                room.AttachedObjects = room.AttachedObjects
                    .Where(idx => map.ContainsKey((int)idx))
                    .Select(idx => (uint)map[(int)idx])
                    .OrderBy(x => x)
                    .ToArray();
            }
        }

        // Remap attachedObjects in portals
        if (mlo.portals != null)
        {
            foreach (var portal in mlo.portals)
            {
                if (portal.AttachedObjects == null) continue;
                portal.AttachedObjects = portal.AttachedObjects
                    .Where(idx => map.ContainsKey((int)idx))
                    .Select(idx => (uint)map[(int)idx])
                    .OrderBy(x => x)
                    .ToArray();
            }
        }

        var groups = BuildGroupSummary(sorted.Select(d => d.Group))
            .Select(g => g.YtypName.Equals(currentYtypFileName, StringComparison.OrdinalIgnoreCase)
                ? ($"{g.YtypName} (internal)", g.Count) : g)
            .ToList();

        return (data.Count(d => d.Resolved), groups);
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    private static List<(XElement Item, string ArchName, string Group)> SortClassified(
        List<(XElement Item, string ArchName, string Group)> input)
        => input
            .OrderBy(d => d.Group == UnresolvedGroup ? 1 : 0)
            .ThenBy(d => d.Group == UnresolvedGroup ? "" : d.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.ArchName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<(string YtypName, int Count)> BuildGroupSummary(IEnumerable<string> keys)
    {
        var result = new List<(string, int)>();
        string? cur = null;
        var count = 0;
        foreach (var k in keys)
        {
            if (cur == null) { cur = k; count = 1; }
            else if (k == cur) count++;
            else { result.Add((cur, count)); cur = k; count = 1; }
        }
        if (cur != null) result.Add((cur, count));
        return result;
    }
}
