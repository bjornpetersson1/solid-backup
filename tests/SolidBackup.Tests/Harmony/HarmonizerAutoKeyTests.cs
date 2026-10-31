using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

public sealed class HarmonizerAutoKeyTests
{
    [Fact]
    public void NoChordUntilTheKeyIsKnown_ThenAChordAtOnce()
    {
        Harmonizer harmonizer = Harmonizer.FromSettings(TestSettings.Default);
        foreach (PitchEstimate estimate in KeyMelodies.Estimates(KeyMelodies.Clip("generated-melody-a-minor")))
        {
            harmonizer.Process(estimate);
            if (harmonizer.Key is null)
            {
                Assert.Null(harmonizer.Chord);
                continue;
            }

            Assert.Equal(new Key(9, Mode.Aeolian), harmonizer.Key);
            Assert.True(estimate.HasNote);
            Assert.NotNull(harmonizer.Chord);
            return;
        }

        Assert.Fail("No key was found.");
    }

    [Fact]
    public void Modulation_ChangesTheKeyTheChordsAreChosenIn()
    {
        Harmonizer harmonizer = Harmonizer.FromSettings(TestSettings.Default);
        List<Key> keys = [];
        foreach (PitchEstimate estimate in KeyMelodies.Estimates(KeyMelodies.Clip("generated-melody-c-to-g-major")))
        {
            harmonizer.Process(estimate);
            if (harmonizer.Key is { } key && (keys.Count == 0 || keys[^1] != key))
            {
                keys.Add(key);
            }
        }

        Assert.Equal([new Key(0, Mode.Ionian), new Key(7, Mode.Ionian)], keys);
        Assert.Equal(7, harmonizer.Chord?.Root);
    }

    [Fact]
    public void PerEstimateCallWithAuto_AllocatesNothing()
    {
        PitchEstimate[] estimates = KeyMelodies.Estimates(KeyMelodies.Clip("generated-melody-c-to-g-major"));
        Run(Harmonizer.FromSettings(TestSettings.Default), estimates);

        Harmonizer harmonizer = Harmonizer.FromSettings(TestSettings.Default);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int changes = Run(harmonizer, estimates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(changes > 1);
        Assert.Equal(new Key(7, Mode.Ionian), harmonizer.Key);
    }

    [Fact]
    public void FixedKey_IsUsedFromTheFirstNote()
    {
        AppSettings settings = TestSettings.Default with { Key = "D", Mode = "Dorian" };

        Harmonizer harmonizer = Harmonizer.FromSettings(settings);

        Assert.Equal(new Key(2, Mode.Dorian), harmonizer.Key);
    }

    private static int Run(Harmonizer harmonizer, PitchEstimate[] estimates)
    {
        int changes = 0;
        foreach (PitchEstimate estimate in estimates)
        {
            if (harmonizer.Process(estimate))
            {
                changes++;
            }
        }

        return changes;
    }
}
