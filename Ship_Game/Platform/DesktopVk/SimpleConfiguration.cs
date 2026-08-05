#if STARDIVE_DESKTOPVK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SDUtils;

namespace Ship_Game.Platform.DesktopVk;

/// <summary>
/// Minimal appSettings store for DesktopVK (avoids System.Configuration.ConfigurationManager package).
/// </summary>
public sealed class SimpleConfiguration
{
    public SimpleAppSettingsSection AppSettings { get; } = new();
    string _path;

    public static SimpleConfiguration OpenExeConfiguration()
    {
        string exe = Environment.ProcessPath ?? "StarDrive";
        string candidate = exe + ".config";
        if (!File.Exists(candidate))
            candidate = Path.Combine(Directory.GetCurrentDirectory(), "StarDrive.dll.config");
        if (!File.Exists(candidate))
            candidate = Path.Combine(Directory.GetCurrentDirectory(), "app.config");
        var cfg = new SimpleConfiguration { _path = candidate };
        if (File.Exists(candidate))
            cfg.LoadFromFile(candidate);
        return cfg;
    }

    public static SimpleConfiguration OpenMapped(string path)
    {
        var cfg = new SimpleConfiguration { _path = path };
        if (File.Exists(path))
            cfg.LoadFromFile(path);
        return cfg;
    }

    public void SaveAs(string path) => SaveAs(path, null);

    public void SaveAs(string path, object _)
    {
        _path = path;
        Save();
    }

    public void Save() => Save(null);

    public void Save(object _)
    {
        string path = _path ?? Path.Combine(Dir.StarDriveAppData, "StarDrive.user.config");
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var adds = AppSettings.Settings.AllKeys.Select(k =>
            new XElement("add",
                new XAttribute("key", k),
                new XAttribute("value", AppSettings.Settings[k]?.Value ?? "")));
        var doc = new XDocument(new XElement("configuration", new XElement("appSettings", adds)));
        doc.Save(path);
        _path = path;
    }

    void LoadFromFile(string path)
    {
        try
        {
            var doc = XDocument.Load(path);
            foreach (var add in doc.Descendants("add"))
            {
                string key = (string)add.Attribute("key");
                string value = (string)add.Attribute("value");
                if (!string.IsNullOrEmpty(key))
                    AppSettings.Settings.Set(key, value ?? "");
            }
        }
        catch
        {
            // ignore corrupt config — defaults remain
        }
    }
}

public sealed class SimpleAppSettingsSection
{
    public SimpleKeyValueCollection Settings { get; } = new();
}

public sealed class SimpleSetting
{
    public string Value { get; set; }
    public SimpleSetting(string value) => Value = value ?? "";
}

public sealed class SimpleKeyValueCollection
{
    readonly Dictionary<string, SimpleSetting> Map = new(StringComparer.OrdinalIgnoreCase);

    public string[] AllKeys
    {
        get
        {
            var keys = new string[Map.Count];
            Map.Keys.CopyTo(keys, 0);
            return keys;
        }
    }

    public SimpleSetting this[string key]
    {
        get => Map.TryGetValue(key, out var s) ? s : null;
        set
        {
            if (value == null) Map.Remove(key);
            else Map[key] = value;
        }
    }

    public void Add(string key, string value) => Map[key] = new SimpleSetting(value);
    public void Set(string key, string value) => Map[key] = new SimpleSetting(value);
}

public static class SimpleConfigurationManager
{
    public static void RefreshSection(string _) { }
}
#endif
