using System.Text.Json;
using System.Text.Json.Nodes;

namespace SolidBackup.Configuration;

public sealed record SettingsLoadResult(AppSettings Settings, string? Message);

public sealed class SettingsStore(string defaultsPath, string userSettingsPath)
{
    private const string FallbackText = "Standardinställningarna används.";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string BundledDefaultsPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static string DefaultUserSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "solid-backup",
        "usersettings.json");

    public SettingsLoadResult Load()
    {
        (JsonObject settingsJson, AppSettings defaults) = LoadDefaults();
        if (!File.Exists(userSettingsPath))
        {
            return new(defaults, null);
        }

        if (TryReadUserSettings() is not { } overrides)
        {
            return new(defaults, $"Inställningsfilen {userSettingsPath} kunde inte läsas. {FallbackText}");
        }

        return ApplyOverrides(settingsJson, overrides, defaults);
    }

    /// <summary>
    /// Saves only the settings that differ from the defaults, so a later change to a default
    /// still reaches users who never chose that setting.
    /// </summary>
    public void Save(AppSettings settings)
    {
        if (SettingsValidator.Validate(settings) is { } error)
        {
            throw new ArgumentException($"Ogiltigt värde för {error.Setting}: {error.Problem}.", nameof(settings));
        }

        JsonObject changes = ChangesFromDefaults(settings);
        string folder = Path.GetDirectoryName(Path.GetFullPath(userSettingsPath))
            ?? throw new InvalidOperationException($"{userSettingsPath} saknar en mapp.");
        Directory.CreateDirectory(folder);

        // Write to a temporary file first, so a crash mid-write never leaves a half-written settings file.
        string temporaryPath = $"{userSettingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, changes.ToJsonString(WriteOptions));
            File.Move(temporaryPath, userSettingsPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private JsonObject ChangesFromDefaults(AppSettings settings)
    {
        JsonObject defaults = ToJson(LoadDefaults().Settings);
        JsonObject changes = ToJson(settings);
        foreach ((string name, JsonNode? value) in defaults)
        {
            if (JsonNode.DeepEquals(changes[name], value))
            {
                changes.Remove(name);
            }
        }

        return changes;
    }

    private (JsonObject Json, AppSettings Settings) LoadDefaults()
    {
        AppSettings settings;
        JsonObject json;
        try
        {
            json = JsonNode.Parse(File.ReadAllText(defaultsPath)) as JsonObject
                ?? throw new JsonException("Filen innehåller inget JSON-objekt.");
            settings = Deserialize(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException($"Standardinställningarna i {defaultsPath} kunde inte läsas.", exception);
        }

        if (SettingsValidator.Validate(settings) is { } error)
        {
            throw new InvalidOperationException(
                $"Standardinställningarna i {defaultsPath} har ett ogiltigt värde för {error.Setting}: {error.Problem}.");
        }

        return (json, settings);
    }

    private SettingsLoadResult ApplyOverrides(JsonObject settingsJson, JsonObject overrides, AppSettings defaults)
    {
        foreach ((string name, JsonNode? value) in overrides)
        {
            settingsJson[name] = value?.DeepClone();
        }

        AppSettings settings;
        try
        {
            settings = Deserialize(settingsJson);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            string setting = (exception as JsonException)?.Path?.TrimStart('$', '.') ?? "okänd inställning";
            return new(defaults, InvalidValueMessage(new SettingsError(setting, "fel typ av värde")));
        }

        return SettingsValidator.Validate(settings) is { } error
            ? new(defaults, InvalidValueMessage(error))
            : new(settings, null);
    }

    private JsonObject? TryReadUserSettings()
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(userSettingsPath)) is not JsonObject overrides)
            {
                return null;
            }

            // JsonObject fills its properties lazily; reading Count does it now, so a duplicate
            // property fails here as a malformed file instead of later during the merge.
            _ = overrides.Count;
            return overrides;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private string InvalidValueMessage(SettingsError error)
    {
        return $"Inställningsfilen {userSettingsPath} har ett ogiltigt värde för {error.Setting}: {error.Problem}. {FallbackText}";
    }

    private static JsonObject ToJson(AppSettings settings)
    {
        return JsonSerializer.SerializeToNode(settings) as JsonObject
            ?? throw new JsonException("Inställningarna kunde inte serialiseras.");
    }

    private static AppSettings Deserialize(JsonObject json)
    {
        return json.Deserialize<AppSettings>()
            ?? throw new JsonException("Inställningarna saknas.");
    }
}
