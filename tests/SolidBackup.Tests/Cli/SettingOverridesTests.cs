using SolidBackup.Cli;
using SolidBackup.Configuration;

namespace SolidBackup.Tests.Cli;

public class SettingOverridesTests
{
    [Fact]
    public void None_KeepTheSettings()
    {
        Assert.True(new SettingOverrides().TryApply(TestSettings.Default, out AppSettings applied, out _));
        Assert.Equal(TestSettings.Default, applied);
    }

    [Fact]
    public void Each_ReplacesItsSetting()
    {
        SettingOverrides overrides = new("Blues", "G", "Dorian", "Organ", "gate,onsets", new RhythmOverrides("Strum", 120, "3/4"));

        Assert.True(overrides.TryApply(TestSettings.Default, out AppSettings applied, out _));

        Assert.Equal(
            TestSettings.Default with
            {
                HarmonyStyle = "Blues",
                Key = "G",
                Mode = "Dorian",
                Sound = "Organ",
                GateOn = true,
                OnsetsOn = true,
                DynamicsOn = false,
                ChordCharacterOn = false,
                RhythmPattern = "Strum",
                Bpm = 120,
                TimeSignature = "3/4",
            },
            applied);
    }

    [Fact]
    public void NoFeatures_SwitchesThemAllOff()
    {
        Assert.True(new SettingOverrides(Features: AccompanyCommand.NoFeatures).TryApply(TestSettings.Default, out AppSettings applied, out _));
        Assert.False(applied.GateOn || applied.DynamicsOn || applied.ChordCharacterOn || applied.OnsetsOn);
    }

    [Fact]
    public void AnUnknownFeature_GivesTheSwedishMessage()
    {
        Assert.False(new SettingOverrides(Features: "eko").TryApply(TestSettings.Default, out _, out string? error));
        Assert.Equal(AccompanyCommand.UnknownFeatureMessage("eko"), error);
    }
}
