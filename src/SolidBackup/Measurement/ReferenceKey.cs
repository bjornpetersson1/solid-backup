using SolidBackup.Harmony;

namespace SolidBackup.Measurement;

/// <summary>The key a generated melody is in from <see cref="StartMs"/> until the next key or the clip's end.</summary>
public sealed record ReferenceKey(double StartMs, Key Key);
