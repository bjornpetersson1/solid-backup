using System.Buffers.Binary;

using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Measurement;
using SolidBackup.Recording;
using SolidBackup.Tests.Accompaniment;
using SolidBackup.Tests.Audio;
using SolidBackup.Tests.Audio.Devices;
using SolidBackup.Tests.Import;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

/// <summary>E8-4: the menu's Importera klipp runs a clip through the chain with the run's settings and saves the result as a recording.</summary>
public sealed class ImportMenuTests : IDisposable
{
    private const float Quantum = 1f / 32767;

    private static readonly DateTime Now = new(2026, 11, 6, 10, 0, 0);

    // Six seconds of the corpus's singing and two of silence, so autoplay has something to do.
    private static readonly float[] Voice = SingingThenSilence();

    // In a fixed key with every level feature and autoplay on, so each one has something to change in eight seconds.
    private static readonly AppSettings AllOn = Chain.Settings([.. LevelFeatures.All.Select(entry => entry.Name)]) with { Autoplay = true };

    // The chords with everything on, computed once: each "changes only" test compares against them.
    private static readonly Lazy<float[]> AllOnChords = new(() => AccompanyCommand.Accompany(Voice, AllOn, noVoice: true));

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"solid-backup-import-menu-{Guid.NewGuid():N}");
    private readonly string _clip;

    // For the tests of the menu's flow, which need a run but not the features at work.
    private readonly string _shortClip;
    private readonly AppSettings _settings;
    private readonly List<MemoryPlayback> _played = [];

    public ImportMenuTests()
    {
        Directory.CreateDirectory(_folder);
        _clip = Path.Combine(_folder, "sång.wav");
        WavWriter.WritePcm16(_clip, 48_000, Voice);
        _settings = AllOn with { RecordingFolder = Path.Combine(_folder, "inspelningar") };
        _shortClip = Path.Combine(_folder, "kort.wav");
        WavWriter.WritePcm16(_shortClip, 48_000, Voice[..(2 * 48_000)]);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void TheResult_IsTheClipLeftAndAccompanysChordsRight_SavedAsARecording()
    {
        TestConsole console = Console(Run);

        Menu(console).Run(_settings);

        string path = Path.Combine(_settings.RecordingFolder, "sång_2026-11-06_10-00-00.wav");
        Assert.Contains(ImportMenu.SavedMessage(path), console.Output);
        byte[] file = File.ReadAllBytes(path);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(22)));
        Assert.Equal(48_000, BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(34)));
        AssertSame(Voice, TestWav.Pcm16Channel(file, 0));
        AssertSame(AllOnChords.Value, TestWav.Pcm16Channel(file, 1));
        Assert.Contains(ImportMenu.ProgressText(1), console.Output);
    }

    public static TheoryData<int> Features => [.. Enumerable.Range(0, LevelFeatures.All.Count)];

    [Theory]
    [MemberData(nameof(Features))]
    public void ALevelFeatureSwitchedOffForTheRun_ChangesOnlyThatFeature(int feature)
    {
        TestConsole console = Console();
        Choose(console, SettingPrompts.FeaturesChoice);
        Press(console, Enumerable.Repeat(ConsoleKey.DownArrow, feature).Append(ConsoleKey.Spacebar).Append(ConsoleKey.Enter));
        Press(console, Run);

        Menu(console).Run(_settings);

        AppSettings off = LevelFeatures.All[feature].Switch(_settings, false);
        float[] chords = TestWav.Pcm16Channel(Saved(), 1);
        AssertSame(AccompanyCommand.Accompany(Voice, off, noVoice: true), chords);
        AssertDiffers(AllOnChords.Value, chords);
    }

    [Fact]
    public void AutoplaySwitchedOffForTheRun_ChangesOnlyAutoplay()
    {
        TestConsole console = Console();
        Choose(console, SettingPrompts.AutoplayChoice);
        Press(console, [ConsoleKey.DownArrow, ConsoleKey.Enter]);
        Press(console, Run);

        Menu(console).Run(_settings);

        float[] chords = TestWav.Pcm16Channel(Saved(), 1);
        AssertSame(AccompanyCommand.Accompany(Voice, _settings with { Autoplay = false }, noVoice: true), chords);
        AssertDiffers(AllOnChords.Value, chords);
    }

    [Fact]
    public void TheResult_CanBeHeardAtOnce_AndIsAmongTheRecordings_AndASecondGetsItsOwnName()
    {
        TestConsole console = Console(Run, _shortClip);

        // Lyssna, Båda; Tillbaka once it has played.
        ImportMenu menu = Menu(console, afterSaving: [ConsoleKey.Enter, ConsoleKey.Enter]);
        menu.Run(_settings);
        console.Input.PushTextWithEnter(Typed(_shortClip));
        Press(console, Run);
        Menu(console).Run(_settings);

        Assert.True(_played[0].Played.Count >= 2 * 48_000);
        Assert.Equal(["kort_2026-11-06_10-00-00", "kort_2026-11-06_10-00-00_2"], RecordingLibrary.List(_settings.RecordingFolder).Select(entry => entry.Name).Order());
    }

    [Fact]
    public void EscWhileRendering_CancelsWithoutLeavingAFile()
    {
        TestConsole console = Console(Run, _shortClip);
        console.Input.PushKey(ConsoleKey.Escape);

        Menu(console).Run(_settings);

        Assert.Contains(ImportMenu.CancelledMessage, console.Output);
        Assert.False(Directory.Exists(_settings.RecordingFolder) && Directory.EnumerateFiles(_settings.RecordingFolder).Any());
    }

    [Fact]
    public void Avbryt_AndEscAtThePath_LeaveWithoutRunning()
    {
        TestConsole console = Console(clip: _shortClip);
        Choose(console, ImportMenu.CancelChoice);
        ImportMenu menu = Menu(console);
        menu.Run(_settings);
        console.Input.PushKey(ConsoleKey.Escape);
        menu.Run(_settings);

        Assert.False(Directory.Exists(_settings.RecordingFolder));
    }

    [Fact]
    public void AClipThatCannotBeImported_IsASwedishError()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushTextWithEnter(Path.Combine(_folder, "saknas.wav"));

        Menu(console).Run(_settings);

        Assert.Contains("finns inte", console.Output);
    }

    [Fact]
    public void TheRunsSettings_AreNeverSaved()
    {
        string userSettings = Path.Combine(_folder, "usersettings.json");
        SettingsStore store = new(SettingsStore.BundledDefaultsPath, userSettings);
        store.Save(_settings);
        string before = File.ReadAllText(userSettings);
        TestConsole console = TestConsoles.Wide();
        Press(console, Enumerable.Repeat(ConsoleKey.DownArrow, MainMenu.Choices.ToList().IndexOf("Importera klipp")).Append(ConsoleKey.Enter));
        console.Input.PushTextWithEnter(Typed(_shortClip));
        Choose(console, SettingPrompts.SoundChoice);
        Press(console, [ConsoleKey.DownArrow, ConsoleKey.Enter]);
        Press(console, Run);
        ImportMenu menu = Menu(console, afterSaving: [ConsoleKey.DownArrow, ConsoleKey.Enter, .. Enumerable.Repeat(ConsoleKey.DownArrow, MainMenu.Choices.Count - 1), ConsoleKey.Enter]);

        new MainMenu(console, store, () => new FakeCatalog(), TestPreflight.Create(), _ => new LiveExit(0, Interrupted: false), _ => { }, menu.Run).Run();

        Assert.Equal(before, File.ReadAllText(userSettings));
        Assert.Single(RecordingLibrary.List(_settings.RecordingFolder));
    }

    // Kör is the run menu's first item.
    private static IEnumerable<ConsoleKey> Run => [ConsoleKey.Enter];

    private static float[] SingingThenSilence()
    {
        float[] singing = WavReader.Read(CorpusDirectory.Locate().WavPath("singing")).Samples;
        return [.. singing.Take(6 * 48_000), .. new float[2 * 48_000]];
    }

    private static void AssertSame(float[] expected, float[] recorded)
    {
        Assert.Equal(expected.Length, recorded.Length);
        int differs = Enumerable.Range(0, recorded.Length).FirstOrDefault(i => Math.Abs(recorded[i] - Math.Clamp(expected[i], -1, 1)) > Quantum, -1);
        Assert.Equal(-1, differs);
    }

    // The run with everything on sounds different somewhere, beyond 16-bit rounding.
    private static void AssertDiffers(float[] allOn, float[] recorded)
    {
        Assert.Contains(Enumerable.Range(0, recorded.Length), i => Math.Abs(recorded[i] - Math.Clamp(allOn[i], -1, 1)) > 4 * Quantum);
    }

    private static void Choose(TestConsole console, string runChoice)
    {
        Press(console, Enumerable.Repeat(ConsoleKey.DownArrow, ImportMenu.RunChoices.ToList().IndexOf(runChoice)).Append(ConsoleKey.Enter));
    }

    private static void Press(TestConsole console, IEnumerable<ConsoleKey> keys)
    {
        foreach (ConsoleKey key in keys)
        {
            console.Input.PushKey(key);
        }
    }

    // A path as the terminal pastes a file dragged in from Finder.
    private static string Typed(string path)
    {
        return path.Replace(" ", "\\ ", StringComparison.Ordinal);
    }

    // A console where the clip's path has been typed, then the given keys pressed.
    private TestConsole Console(IEnumerable<ConsoleKey>? then = null, string? clip = null)
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushTextWithEnter(Typed(clip ?? _clip));
        Press(console, then ?? []);
        return console;
    }

    private byte[] Saved()
    {
        return File.ReadAllBytes(Assert.Single(Directory.GetFiles(_settings.RecordingFolder)));
    }

    // While it renders, the menu takes every key pressed, looking for Esc, so the keys for what comes after are pressed
    // once the result is saved (when it asks the time to name it); by default Tillbaka at "Vill du lyssna?". The
    // player takes keys too, so the playback presses Tillbaka there once it has played.
    private ImportMenu Menu(TestConsole console, ConsoleKey[]? afterSaving = null)
    {
        MemoryPlayback playback = new()
        {
            WhenFinished = () => Press(console, [ConsoleKey.End, ConsoleKey.Enter]),
        };
        _played.Add(playback);
        RecordingsMenu recordings = new(console, _ => playback, pollInterval: TimeSpan.Zero);
        return new ImportMenu(
            console,
            new FakeConverter(),
            recordings,
            () =>
            {
                Press(console, afterSaving ?? [ConsoleKey.DownArrow, ConsoleKey.Enter]);
                return Now;
            },
            pollInterval: TimeSpan.Zero);
    }
}
