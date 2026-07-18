using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CodeWalker.GameFiles;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Salaros.Configuration;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace YtypGrouper;

public partial class MainWindow : Window
{
    private static string? _gtaPath;
    private string? _outputDir;
    private GameFileCache? _cache;
    private FileProcessor? _processor;

    private readonly string _configPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

    private static readonly SolidColorBrush BrushOk    = new(Color.Parse("#88DD88"));
    private static readonly SolidColorBrush BrushError = new(Color.Parse("#FF6644"));
    private static readonly SolidColorBrush BrushDim   = new(Color.Parse("#AA8880"));

    public MainWindow()
    {
        InitializeComponent();
        BtnPickFile.IsEnabled = false;

        var cfg = new ConfigParser(_configPath);
        var savedOutput = cfg.GetValue("CONFIG", "OutputDir");
        if (!string.IsNullOrEmpty(savedOutput) && Directory.Exists(savedOutput))
            SetOutputDir(savedOutput);
    }

    // -------------------------------------------------------------------------
    // Menu
    // -------------------------------------------------------------------------

    private void MiExit_OnClick(object? sender, RoutedEventArgs e)
        => Environment.Exit(0);

    // -------------------------------------------------------------------------
    // GTA V path + GameFileCache
    // -------------------------------------------------------------------------

    private async void BtnGTAPath_OnClick(object? sender, RoutedEventArgs e)
    {
        var cfg = new ConfigParser(_configPath);
        var savedPath = cfg.GetValue("CONFIG", "GTA5Path");

        if (!string.IsNullOrEmpty(savedPath) && IsValidGtaPath(savedPath))
        {
            _gtaPath = savedPath;
        }
        else
        {
            var picked = await GetTopLevel(this)!.StorageProvider
                .OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select your GTA V folder",
                    AllowMultiple = false
                });
            if (picked.Count == 0) return;
            _gtaPath = picked[0].Path.LocalPath;
            cfg.SetValue("CONFIG", "GTA5Path", _gtaPath);
            cfg.Save();
        }

        if (!IsValidGtaPath(_gtaPath))
        {
            await ShowError("Invalid Path", "GTA5.exe not found in the selected folder.");
            return;
        }

        // Custom dialog pour respecter la DA
        var modsDialog = new ConfirmDialog("Mods", "Enable mods folder?");
        await modsDialog.ShowDialog(this);
        var loadMods = modsDialog.Result;

        GTA5Keys.LoadFromPath(_gtaPath);
        SetStatus("Loading game cache…");

        _cache = new GameFileCache(
            int.MaxValue, 10, _gtaPath, "mp2024_01_g9ec", loadMods, "Installers;_CommonRedist")
        {
            LoadAudio    = false,
            LoadVehicles = false,
            LoadPeds     = false
        };

        await Task.Run(() => _cache.Init(SetStatus, msg => Console.Error.WriteLine(msg)));

        if (!_cache.IsInited) { SetStatus("Cache load failed."); return; }

        _processor = new FileProcessor(_cache);
        UpdatePickButton();
        BtnGTAPath.IsEnabled = false;
        SetStatus("Game cache loaded ✓");
    }

    // -------------------------------------------------------------------------
    // Output folder picker
    // -------------------------------------------------------------------------

    private async void BtnPickOutput_OnClick(object? sender, RoutedEventArgs e)
    {
        var picked = await GetTopLevel(this)!.StorageProvider
            .OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select output folder",
                AllowMultiple = false
            });
        if (picked.Count == 0) return;

        var dir = picked[0].Path.LocalPath;
        SetOutputDir(dir);

        var cfg = new ConfigParser(_configPath);
        cfg.SetValue("CONFIG", "OutputDir", dir);
        cfg.Save();
    }

    private void SetOutputDir(string dir)
    {
        _outputDir = dir;
        TbOutputDir.Text = dir;
        TbOutputDir.Foreground = BrushOk;
        UpdatePickButton();
    }

    // -------------------------------------------------------------------------
    // File picker + sort
    // -------------------------------------------------------------------------

    private async void BtnPickFile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_processor == null || _outputDir == null) return;

        var picked = await GetTopLevel(this)!.StorageProvider
            .OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open file to sort",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("All supported files (*.ymap, *.ytyp, *.xml)")
                    {
                        Patterns = ["*.ymap", "*.ytyp", "*.xml"]
                    },
                    new FilePickerFileType("YMAP binary (*.ymap)")
                    {
                        Patterns = ["*.ymap"]
                    },
                    new FilePickerFileType("YTYP binary (*.ytyp)")
                    {
                        Patterns = ["*.ytyp"]
                    },
                    new FilePickerFileType("CodeWalker XML (*.xml)")
                    {
                        Patterns = ["*.xml"]
                    }
                ]
            });

        if (picked.Count == 0) return;
        var filePath = picked[0].Path.LocalPath;

        BtnPickFile.IsEnabled = false;
        TbResult.Foreground   = BrushDim;
        TbResult.Text         = "Sorting…";

        try
        {
            var result = await Task.Run(() => _processor.ProcessFile(filePath, _outputDir));
            TbResult.Text       = FormatResult(result);
            TbResult.Foreground = BrushOk;
        }
        catch (XmlBinaryFormatException ex)
        {
            await ShowError("Binary Format Detected", ex.Message);
            SetResultError("Cancelled.");
        }
        catch (XmlStructureException ex)
        {
            await ShowError("Structure Error", ex.Message);
            SetResultError("Cancelled.");
        }
        catch (System.Xml.XmlException ex)
        {
            await ShowError("XML Error", $"The file contains XML syntax errors.\n{ex.Message}");
            SetResultError("Cancelled.");
        }
        catch (Exception ex)
        {
            await ShowError("Error", ex.Message);
            SetResultError("Cancelled.");
        }
        finally
        {
            BtnPickFile.IsEnabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void UpdatePickButton()
        => BtnPickFile.IsEnabled = _processor != null && _outputDir != null;

    private static string FormatResult(SortResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Done! {r.TotalSorted} entity/entities sorted by source YTYP.");
        sb.AppendLine($"Saved to: {r.OutputPath}");

        if (r.Groups.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Breakdown by YTYP:");
            foreach (var (name, count) in r.Groups)
                sb.AppendLine($"  • {name} : {count}");
        }

        if (r.TotalUnresolved > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"⚠  {r.TotalUnresolved} unresolved (missing mod or unknown archetype) — placed at end.");
        }

        return sb.ToString().TrimEnd();
    }

    private void SetResultError(string msg)
    {
        TbResult.Text       = msg;
        TbResult.Foreground = BrushError;
    }

    private static bool IsValidGtaPath(string? path)
        => !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, "GTA5.exe"));

    private void SetStatus(string text)
        => Dispatcher.UIThread.InvokeAsync(() => LabelCache.Text = text);

    // Erreurs non critiques — on garde MessageBox.Avalonia pour les erreurs
    // car elles n'ont pas de choix Yes/No, l'utilisateur voit juste OK.
    private static async Task ShowError(string title, string msg)
        => await MessageBoxManager.GetMessageBoxStandard(
               title, msg, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
}
