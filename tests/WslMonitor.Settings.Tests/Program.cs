// Portable harness for the settings file (CONF-3, CONF-4): the kit's real JsonSettingsStore with the
// app's real schema steps (SettingsMigrations) and settings class, run against real files in a
// temporary folder. Each scenario writes the file a real install could have, loads it the way
// SettingsManager does, and checks what the user would get back. Exits non-zero on the first failure.
using System.Text.Json.Nodes;
using WinUIAppKit.Settings;
using WslMonitor.Classes;
using WslMonitor.ViewModels;

var scenarios = new List<(string Name, Action<string> Body)>
{
    ("with no file, it is a first run and the file is written with a schema version", FirstRun),
    ("a 1.0.0 file keeps every value and is not a first run", UnversionedFileKeepsValues),
    ("a saved false for a setting that defaults to true stays false", FalseStaysFalse),
    ("a file that isn't JSON is kept as .bad, defaults are written, and it is not a first run", NotJson),
    ("a current file loads without a backup", CurrentVersion),
    ("a file from a newer version is backed up and loaded as far as this version understands", NewerVersion),
    ("Reset keeps the old file and writes defaults", ResetKeepsOldFile),
};

string root = Path.Combine(Path.GetTempPath(), "tray-control-for-wsl-settings-tests-" + Guid.NewGuid().ToString("N"));
try
{
    for (int i = 0; i < scenarios.Count; i++)
    {
        var (name, body) = scenarios[i];
        string folder = Path.Combine(root, i.ToString());
        Directory.CreateDirectory(folder);
        try
        {
            body(Path.Combine(folder, "settings.json"));
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL: " + name);
            Console.WriteLine("  " + ex.Message);
            return 1;
        }

        Console.WriteLine("PASS: " + name);
    }
}
finally
{
    Directory.Delete(root, recursive: true);
}

Console.WriteLine($"All {scenarios.Count} scenarios passed.");
return 0;

// ── Scenarios ───────────────────────────────────────────────────────────

void FirstRun(string path)
{
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.FirstRun, "the outcome is FirstRun");
    Check(File.Exists(path), "the file was written");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "the file carries the current schema version");
    Check(result.Settings.Startup && !result.Settings.ShowSystemDistros, "the defaults apply");
}

void UnversionedFileKeepsValues(string path)
{
    // As written by every build before schema versions: the app's own "Version": 1 and every setting.
    string original = """
        {
          "Version": 1,
          "AppTheme": 2,
          "Language": "fr-CA",
          "Startup": false,
          "ShowSystemDistros": true,
          "DetailsWindow": { "X": 100, "Y": 80, "Width": 1100, "Height": 760 }
        }
        """;
    File.WriteAllText(path, original);

    var result = Store(path).Load();
    var s = result.Settings;

    Check(result.Outcome == SettingsLoadOutcome.Migrated, "the outcome is Migrated, so the app does not treat it as a first run");
    Check(File.ReadAllText(path + ".v0.bak") == original, "the original file is kept as settings.json.v0.bak");
    Check((int)s.AppTheme == 2 && s.Language == "fr-CA", "theme and language are kept");
    Check(!s.Startup && s.ShowSystemDistros, "Start with Windows and the Docker distros setting are kept");
    Check(s.DetailsWindow is { X: 100, Y: 80, Width: 1100, Height: 760 }, "the window position is kept");

    var saved = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    Check(!saved.ContainsKey("Version"), "the old Version marker is gone");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "the file carries the current schema version");
}

void FalseStaysFalse(string path)
{
    File.WriteAllText(path, """{ "Version": 1, "Startup": false }""");
    var store = Store(path);
    store.Load();

    var reloaded = store.Load();
    Check(reloaded.Outcome == SettingsLoadOutcome.Loaded, "the second load finds a current file");
    Check(!reloaded.Settings.Startup, "false is still false after saving and loading again");
}

void NotJson(string path)
{
    File.WriteAllText(path, "not json {");
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.Unreadable, "the outcome is Unreadable, not FirstRun");
    Check(File.ReadAllText(path + ".bad") == "not json {", "the unreadable file is kept as .bad");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "defaults were written with the current schema version");
}

void CurrentVersion(string path)
{
    var store = Store(path);
    store.Load();
    var result = store.Load();

    Check(result.Outcome == SettingsLoadOutcome.Loaded, "the outcome is Loaded");
    Check(result.BackupPath == null && Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "no backup was made");
}

void NewerVersion(string path)
{
    string newer = """{ "SchemaVersion": 99, "AppTheme": 1, "SomethingNew": true }""";
    File.WriteAllText(path, newer);
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.NewerVersion, "the outcome is NewerVersion");
    Check(File.ReadAllText(path + ".v99.bak") == newer, "the newer file is kept as .v99.bak");
    Check((int)result.Settings.AppTheme == 1, "what this version understands is loaded");
}

void ResetKeepsOldFile(string path)
{
    var store = Store(path);
    File.WriteAllText(path, """{ "Version": 1, "AppTheme": 2, "Language": "de-DE", "ShowSystemDistros": true }""");
    store.Load();

    var result = store.Reset();
    var resetFiles = Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.json.*.reset");

    Check(result.Outcome == SettingsLoadOutcome.Reset, "the outcome is Reset");
    Check(resetFiles.Length == 1 && File.ReadAllText(resetFiles[0]).Contains("de-DE"), "the previous file is kept as .reset");
    Check((int)result.Settings.AppTheme == 0 && result.Settings.Language == null && !result.Settings.ShowSystemDistros, "defaults are back");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "the new file carries the current schema version");
}

// ── Helpers ─────────────────────────────────────────────────────────────

// The store as SettingsManager creates it, at a test path.
static JsonSettingsStore<UserSettings> Store(string path) => new(path, SettingsMigrations.Steps);

static int? FileVersion(string path) =>
    JsonNode.Parse(File.ReadAllText(path))?[JsonSettingsStore<UserSettings>.SchemaVersionProperty]?.GetValue<int>();

static void Check(bool condition, string what)
{
    if (!condition)
    {
        throw new Exception(what);
    }
}
