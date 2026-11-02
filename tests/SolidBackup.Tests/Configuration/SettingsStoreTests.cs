using System.Runtime.Versioning;
using System.Text.Json.Nodes;

using SolidBackup.Configuration;

namespace SolidBackup.Tests.Configuration;

public sealed class SettingsStoreTests : IDisposable
{
    private const string FallbackText = "Standardinställningarna används.";

    private static readonly AppSettings ExpectedDefaults = TestSettings.Default;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests", Guid.NewGuid().ToString("N"));
    private readonly string _userFolder;
    private readonly string _userSettingsPath;

    public SettingsStoreTests()
    {
        _userFolder = Path.Combine(_folder, "nested");
        _userSettingsPath = Path.Combine(_userFolder, "usersettings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutUserFile_ReturnsAppSettingsDefaultsWithoutMessage()
    {
        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(ExpectedDefaults, result.Settings);
        Assert.Null(result.Message);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("""{ "BufferSize": 512, "BufferSize": 256 }""")]
    public void Load_MalformedUserFile_ReturnsDefaultsWithSwedishMessage(string content)
    {
        WriteUserFile(content);

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(ExpectedDefaults, result.Settings);
        Assert.Equal($"Inställningsfilen {_userSettingsPath} kunde inte läsas. {FallbackText}", result.Message);
        Assert.Equal(content, File.ReadAllText(_userSettingsPath));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void Load_UnreadableUserFile_ReturnsDefaultsWithSwedishMessage()
    {
        WriteUserFile("""{ "BufferSize": 512 }""");
        File.SetUnixFileMode(_userSettingsPath, UnixFileMode.None);

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(ExpectedDefaults, result.Settings);
        Assert.Equal($"Inställningsfilen {_userSettingsPath} kunde inte läsas. {FallbackText}", result.Message);
    }

    [Theory]
    [InlineData("""{ "LowestNote": "C6", "HighestNote": "E2" }""", "LowestNote")]
    [InlineData("""{ "LowestNote": "H2" }""", "LowestNote")]
    [InlineData("""{ "HighestNote": "C" }""", "HighestNote")]
    [InlineData("""{ "HighestNote": null }""", "HighestNote")]
    [InlineData("""{ "BufferSize": 300 }""", "BufferSize")]
    [InlineData("""{ "BufferSize": 0 }""", "BufferSize")]
    [InlineData("""{ "BufferSize": "stor" }""", "BufferSize")]
    [InlineData("""{ "InputDevice": null }""", "InputDevice")]
    [InlineData("""{ "OutputDevice": null }""", "OutputDevice")]
    [InlineData("""{ "SampleRate": 96000 }""", "SampleRate")]
    [InlineData("""{ "SampleRate": "snabb" }""", "SampleRate")]
    [InlineData("""{ "PitchConfidenceThreshold": 1.5 }""", "PitchConfidenceThreshold")]
    [InlineData("""{ "PitchConfidenceThreshold": -0.1 }""", "PitchConfidenceThreshold")]
    [InlineData("""{ "GateThresholdDbfs": 3 }""", "GateThresholdDbfs")]
    [InlineData("""{ "LatencyTargetMs": 0 }""", "LatencyTargetMs")]
    [InlineData("""{ "GateHysteresisDb": 0 }""", "GateHysteresisDb")]
    [InlineData("""{ "GateThresholdDbfs": -118 }""", "GateThresholdDbfs")]
    [InlineData("""{ "GateHysteresisDb": 41 }""", "GateHysteresisDb")]
    [InlineData("""{ "GateHoldMs": -5 }""", "GateHoldMs")]
    [InlineData("""{ "GateHoldMs": 1001 }""", "GateHoldMs")]
    [InlineData("""{ "OnsetThresholdDb": 0 }""", "OnsetThresholdDb")]
    [InlineData("""{ "OnsetThresholdDb": 61 }""", "OnsetThresholdDb")]
    [InlineData("""{ "OnsetMinIntervalMs": 0 }""", "OnsetMinIntervalMs")]
    [InlineData("""{ "OnsetMinIntervalMs": 2001 }""", "OnsetMinIntervalMs")]
    [InlineData("""{ "LowestNote": "E+2" }""", "LowestNote")]
    [InlineData("""{ "HarmonyStyle": "Kyrko" }""", "HarmonyStyle")]
    [InlineData("""{ "HarmonyStyle": "church" }""", "HarmonyStyle")]
    [InlineData("""{ "HarmonyStyle": "1" }""", "HarmonyStyle")]
    [InlineData("""{ "HarmonyStyle": null }""", "HarmonyStyle")]
    [InlineData("""{ "Key": "H" }""", "Key")]
    [InlineData("""{ "Key": "C4" }""", "Key")]
    [InlineData("""{ "Key": "Auto" }""", "Key")]
    [InlineData("""{ "Key": null }""", "Key")]
    [InlineData("""{ "Mode": "Dorisk" }""", "Mode")]
    [InlineData("""{ "Mode": null }""", "Mode")]
    [InlineData("""{ "MinNoteForChangeMs": 0 }""", "MinNoteForChangeMs")]
    [InlineData("""{ "MinChordDurationMs": -1 }""", "MinChordDurationMs")]
    [InlineData("""{ "PhraseResetMs": 0 }""", "PhraseResetMs")]
    [InlineData("""{ "KeyChangeSeconds": 0 }""", "KeyChangeSeconds")]
    [InlineData("""{ "KeyChangeSeconds": -4 }""", "KeyChangeSeconds")]
    [InlineData("""{ "KeyFirstSeconds": 0 }""", "KeyFirstSeconds")]
    [InlineData("""{ "Sound": "Piano" }""", "Sound")]
    [InlineData("""{ "Sound": "pad" }""", "Sound")]
    [InlineData("""{ "Sound": null }""", "Sound")]
    public void Load_InvalidValue_IgnoresWholeFileAndNamesSetting(string content, string setting)
    {
        WriteUserFile(content);

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(ExpectedDefaults, result.Settings);
        Assert.NotNull(result.Message);
        Assert.StartsWith($"Inställningsfilen {_userSettingsPath} har ett ogiltigt värde för {setting}: ", result.Message);
        Assert.EndsWith(FallbackText, result.Message);
        Assert.Equal(content, File.ReadAllText(_userSettingsPath));
    }

    [Fact]
    public void Load_InvalidValueBesideValidOne_IgnoresTheValidOneToo()
    {
        WriteUserFile("""{ "LatencyTargetMs": 40, "BufferSize": 300 }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(ExpectedDefaults, result.Settings);
    }

    [Fact]
    public void Save_ThenLoadWithNewStore_ReturnsSavedSettings()
    {
        AppSettings saved = new()
        {
            LowestNote = "A2",
            HighestNote = "F#5",
            BufferSize = ExpectedDefaults.BufferSize * 2,
            SampleRate = 44_100,
            InputDevice = "USB Audio",
            OutputDevice = "Externa hörlurar",
            PitchConfidenceThreshold = 0.6,
            GateThresholdDbfs = -42.5,
            GateHysteresisDb = 3,
            GateHoldMs = 80,
            OnsetThresholdDb = 12,
            OnsetMinIntervalMs = 150,
            LatencyTargetMs = 30,
            HarmonyStyle = "Campfire",
            Key = "Bb",
            Mode = "Dorian",
            MinNoteForChangeMs = 80,
            MinChordDurationMs = 300,
            PhraseResetMs = 3000,
            KeyChangeSeconds = 6,
            KeyFirstSeconds = 3,
            Sound = "Organ",
        };

        CreateStore().Save(saved);
        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(saved, result.Settings);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Load_PartialUserFile_KeepsDefaultsForOmittedSettings()
    {
        WriteUserFile("""{ "BufferSize": 512 }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(ExpectedDefaults with { BufferSize = 512 }, result.Settings);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Save_InvalidSettings_ThrowsAndWritesNothing()
    {
        AppSettings invalid = ExpectedDefaults with { BufferSize = 300 };

        Assert.Throws<ArgumentException>(() => CreateStore().Save(invalid));
        Assert.False(File.Exists(_userSettingsPath));
    }

    [Fact]
    public void Save_WritesOnlySettingsThatDifferFromDefaults()
    {
        int bufferSize = ExpectedDefaults.BufferSize * 2;
        CreateStore().Save(ExpectedDefaults with { BufferSize = bufferSize });

        Assert.Equal($$"""{"BufferSize":{{bufferSize}}}""", JsonNode.Parse(File.ReadAllText(_userSettingsPath))?.ToJsonString());
        Assert.Empty(Directory.GetFiles(_userFolder, "*.tmp"));
    }

    [Fact]
    public void Save_NonFiniteGateThreshold_Throws()
    {
        AppSettings invalid = ExpectedDefaults with { GateThresholdDbfs = double.NaN };

        Assert.Throws<ArgumentException>(() => CreateStore().Save(invalid));
    }

    [Fact]
    public void Load_MissingDefaultsFile_ThrowsSwedishMessage()
    {
        string missingDefaults = Path.Combine(_folder, "appsettings.json");
        SettingsStore store = new(missingDefaults, _userSettingsPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(store.Load);

        Assert.Equal($"Standardinställningarna i {missingDefaults} kunde inte läsas.", exception.Message);
    }

    [Fact]
    public void DefaultUserSettingsPath_IsInApplicationSupport()
    {
        string expectedEnd = Path.Combine("Library", "Application Support", "solid-backup", "usersettings.json");

        Assert.EndsWith(expectedEnd, SettingsStore.DefaultUserSettingsPath);
    }

    private SettingsStore CreateStore()
    {
        return new SettingsStore(SettingsStore.BundledDefaultsPath, _userSettingsPath);
    }

    private void WriteUserFile(string content)
    {
        Directory.CreateDirectory(_userFolder);
        File.WriteAllText(_userSettingsPath, content);
    }
}
