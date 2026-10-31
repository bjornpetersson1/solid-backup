using System.Runtime.Versioning;
using System.Text.Json.Nodes;

using SolidBackup.Configuration;

namespace SolidBackup.Tests.Configuration;

public sealed class SettingsStoreTests : IDisposable
{
    private const string FallbackText = "Standardinställningarna används.";

    private static readonly AppSettings ExpectedDefaults = new()
    {
        LowestNote = "E2",
        HighestNote = "C6",
        BufferSize = 64,
        GateThresholdDbfs = -50,
        LatencyTargetMs = 50,
    };

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
    [InlineData("""{ "GateThresholdDbfs": 3 }""", "GateThresholdDbfs")]
    [InlineData("""{ "LatencyTargetMs": 0 }""", "LatencyTargetMs")]
    [InlineData("""{ "LowestNote": "E+2" }""", "LowestNote")]
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
            GateThresholdDbfs = -42.5,
            LatencyTargetMs = 30,
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
