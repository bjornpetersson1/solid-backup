namespace SolidBackup.Harmony;

/// <summary>Why valid settings cannot give a <see cref="Harmonizer"/> yet.</summary>
public enum HarmonizerProblem
{
    None,

    /// <summary>The harmony style is accepted by the settings but its rules are not built yet.</summary>
    StyleNotBuilt,

    /// <summary>The key is <c>auto</c>, which needs key detection (E4-3).</summary>
    AutoKey,
}
