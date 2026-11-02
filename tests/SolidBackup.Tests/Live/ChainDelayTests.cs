using System.Diagnostics;

using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Live;
using SolidBackup.Synthesis;

namespace SolidBackup.Tests.Live;

public class ChainDelayTests
{
    [Fact]
    public void WithTheDefaults_LiesBetweenHalfThePitchWindowAndTheTarget()
    {
        AppSettings settings = TestSettings.Default;
        double halfWindowMs = PitchDetector.FromSettings(settings).WindowLength / 2.0 * 1000 / settings.SampleRate;

        double? delay = ChainDelay.MeasureMs(settings);

        Assert.NotNull(delay);
        Assert.InRange(delay.Value, halfWindowMs, settings.LatencyTargetMs);
    }

    [Fact]
    public void IsDeterministic()
    {
        Assert.Equal(ChainDelay.MeasureMs(TestSettings.Default), ChainDelay.MeasureMs(TestSettings.Default));
    }

    [Theory]
    [InlineData("Organ")]
    [InlineData("Pluck")]
    public void ThePadsSlowAttack_IsHeardAtMostAnEdgeLater(string sound)
    {
        // Every envelope starts on the same sample; the pad's 200 ms attack takes a little longer to reach −60 dBFS.
        double? pad = ChainDelay.MeasureMs(TestSettings.Default);
        double? other = ChainDelay.MeasureMs(TestSettings.Default with { Sound = sound });

        Assert.NotNull(pad);
        Assert.NotNull(other);
        Assert.InRange(pad.Value - other.Value, 0, SoundPreset.MinEdgeMs);
    }

    [Theory]
    [InlineData("Arpeggio")]
    [InlineData("Strum")]
    public void IsMeasuredWithHeld_WhateverThePattern(string pattern)
    {
        Assert.Equal(ChainDelay.MeasureMs(TestSettings.Default), ChainDelay.MeasureMs(TestSettings.Default with { RhythmPattern = pattern }));
    }

    [Fact]
    public void AToneTheGateNeverLetsThrough_IsUnknown()
    {
        Assert.Null(ChainDelay.MeasureMs(TestSettings.Default with { GateThresholdDbfs = -6 }));
    }
}

/// <summary>
/// The time budget of <see cref="ChainDelay.MeasureMs"/>, run alone: live mode measures before its audio starts, with
/// nothing else competing for the CPU, so other test classes running in parallel would only measure their own load.
/// </summary>
[Collection(nameof(ChainDelayTimingTests))]
public class ChainDelayTimingTests
{
    [Theory]
    [InlineData(-42)]
    [InlineData(-6)]
    public void TakesUnder100Ms_AlsoWhenNoChordComes(double gateThresholdDbfs)
    {
        AppSettings settings = TestSettings.Default with { GateThresholdDbfs = gateThresholdDbfs };
        ChainDelay.MeasureMs(settings);
        Stopwatch watch = Stopwatch.StartNew();

        ChainDelay.MeasureMs(settings);

        Assert.InRange(watch.ElapsedMilliseconds, 0, 100);
    }
}

[CollectionDefinition(nameof(ChainDelayTimingTests), DisableParallelization = true)]
public class ChainDelayTimingCollection
{
}
