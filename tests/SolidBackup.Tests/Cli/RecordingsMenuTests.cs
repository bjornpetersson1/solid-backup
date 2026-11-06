using SolidBackup.Audio;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Recording;
using SolidBackup.Tests.Audio;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

/// <summary>E8-3: the menu's Inspelningar lists, plays, renames and deletes the recordings in the recordings folder.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class RecordingsMenuTests : IDisposable
{
    private const int SampleRate = 48_000;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"solid-backup-recordings-{Guid.NewGuid():N}");
    private readonly AppSettings _settings;
    private MemoryPlayback _playback = new();
    private readonly List<AppSettings> _opened = [];

    public RecordingsMenuTests()
    {
        Directory.CreateDirectory(_folder);
        _settings = TestSettings.Default with { RecordingFolder = _folder };
    }

    public void Dispose()
    {
        File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void TheList_ShowsEveryRecordingNewestFirst_WithItsDateAndLength_AndUnreadableOnes()
    {
        Stereo("äldst", seconds: 3, written: new DateTime(2026, 11, 1, 10, 0, 0));
        Stereo("nyast", seconds: 75, written: new DateTime(2026, 11, 3, 18, 30, 0));
        Stereo("mitten", seconds: 1, written: new DateTime(2026, 11, 2, 9, 15, 0));
        File.WriteAllText(Path.Combine(_folder, "trasig.wav"), "inte en WAV-fil");
        File.SetLastWriteTime(Path.Combine(_folder, "trasig.wav"), new DateTime(2026, 10, 1));
        File.WriteAllText(Path.Combine(_folder, "anteckning.txt"), "ingen inspelning");

        TestConsole console = Console(ConsoleKey.End, ConsoleKey.Enter);
        Menu(console).Run(_settings);

        Assert.Equal(["nyast", "mitten", "äldst", "trasig"], RecordingLibrary.List(_folder).Select(entry => entry.Name));
        string output = console.Output;
        Assert.Contains("nyast  2026-11-03 18:30  1:15", output);
        Assert.Contains("äldst  2026-11-01 10:00  0:03", output);
        Assert.Contains($"trasig  2026-10-01 00:00  {RecordingsMenu.UnreadableLength}", output);
        Assert.Contains(RecordingsMenu.BackChoice, output);
        Assert.True(output.IndexOf("nyast", StringComparison.Ordinal) < output.IndexOf("mitten", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEmptyOrMissingFolder_SaysSo_AndGoesBack()
    {
        TestConsole console = Console();
        Menu(console).Run(_settings);
        Menu(console).Run(_settings with { RecordingFolder = Path.Combine(_folder, "saknas") });

        Assert.Contains(RecordingsMenu.EmptyMessage(_folder), console.Output);
        Assert.Contains(RecordingsMenu.EmptyMessage(Path.Combine(_folder, "saknas")), console.Output);
    }

    [Theory]
    [InlineData(0, 0.125f)]
    [InlineData(1, 0.25f)]
    [InlineData(2, 0f)]
    public void Listening_PlaysTheChosenChannels_AtTheFilesRate(int channelChoice, float expected)
    {
        // The microphone at 0.25 on the left, the chords at 0 on the right: both mixed is their average.
        WriteStereo("spår", left: 0.25f, right: 0f, frames: 3 * 22_050, sampleRate: 22_050);
        List<ConsoleKey> keys = [ConsoleKey.Enter, ConsoleKey.Enter];
        keys.AddRange(Enumerable.Repeat(ConsoleKey.DownArrow, channelChoice));
        keys.Add(ConsoleKey.Enter);
        TestConsole console = Console([.. keys]);
        _playback = BackAfterPlaying(console);

        Menu(console).Run(_settings);

        Assert.True(_playback.Played.Count >= 3 * 22_050);
        Assert.All(_playback.Played.Take(3 * 22_050), sample => Assert.Equal(expected, sample, 1e-4f));
        Assert.Equal(22_050, Assert.Single(_opened).SampleRate);
        Assert.True(_playback.Stopped);
        Assert.True(_playback.Disposed);
    }

    [Fact]
    public void AMonoFile_IsPlayedWithoutAskingForChannels()
    {
        byte[] wav = TestWav.Pcm16(SampleRate, 1, [.. Enumerable.Repeat(0.5f, SampleRate)]);
        File.WriteAllBytes(Path.Combine(_folder, "mono.wav"), wav);
        TestConsole console = Console(ConsoleKey.Enter, ConsoleKey.Enter);
        _playback = BackAfterPlaying(console);

        Menu(console).Run(_settings);

        Assert.DoesNotContain(RecordingsMenu.MicrophoneOnly, console.Output);
        Assert.All(_playback.Played.Take(SampleRate), sample => Assert.Equal(0.5f, sample, 1e-4f));
    }

    [Fact]
    public void TheKeys_PauseSeekAndStop()
    {
        Stereo("lång", seconds: 60, written: DateTime.Now);
        TestConsole console = Console(
            ConsoleKey.Enter, ConsoleKey.Enter, ConsoleKey.Enter,
            ConsoleKey.RightArrow, ConsoleKey.RightArrow, ConsoleKey.LeftArrow, ConsoleKey.LeftArrow, ConsoleKey.LeftArrow,
            ConsoleKey.Spacebar, ConsoleKey.Q,
            ConsoleKey.End, ConsoleKey.Enter, ConsoleKey.End, ConsoleKey.Enter);

        Menu(console).Run(_settings);

        // Forward twice, back three times, clamped at the start.
        int seek = PlaybackScreen.SeekSeconds * SampleRate;
        Assert.Equal([seek, 2 * seek, seek, 0, 0], _playback.Seeks);
        Assert.True(_playback.Paused);
        Assert.Empty(_playback.Played);
        Assert.True(_playback.Stopped);
        Assert.Contains("0:00 / 1:00", console.Output);
        Assert.Contains(PlaybackScreen.KeysText, console.Output);
    }

    [Fact]
    public void PlayingToTheEnd_ReturnsToTheList()
    {
        Stereo("kort", seconds: 1, written: DateTime.Now);
        TestConsole console = Console(ConsoleKey.Enter, ConsoleKey.Enter, ConsoleKey.Enter);
        _playback = BackAfterPlaying(console);

        Menu(console).Run(_settings);

        Assert.Equal(SampleRate, _playback.Played.Take(SampleRate).Count());
        Assert.Contains("0:01 / 0:01", console.Output);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deleting_AsksFirst(bool yes)
    {
        string path = Stereo("bort", seconds: 1, written: DateTime.Now);
        TestConsole console = Console(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter(yes ? "j" : "n");
        if (!yes)
        {
            console.Input.PushKey(ConsoleKey.End);
            console.Input.PushKey(ConsoleKey.Enter);
        }

        Menu(console).Run(_settings);

        Assert.Contains("Ta bort bort?", console.Output);
        Assert.Equal(!yes, File.Exists(path));
    }

    [Fact]
    public void Renaming_AddsWav()
    {
        string path = Stereo("gammal", seconds: 1, written: DateTime.Now);
        TestConsole console = Console(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter("blues-test");
        console.Input.PushKey(ConsoleKey.End);
        console.Input.PushKey(ConsoleKey.Enter);

        Menu(console).Run(_settings);

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(Path.Combine(_folder, "blues-test.wav")));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("..")]
    [InlineData("annan")]
    public void ARefusedName_IsAskedAgain_AndAnEmptyAnswerCancels(string refused)
    {
        string path = Stereo("gammal", seconds: 1, written: DateTime.Now);
        Stereo("annan", seconds: 1, written: DateTime.Now.AddHours(-1));
        TestConsole console = Console(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter(refused);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.End);
        console.Input.PushKey(ConsoleKey.Enter);

        Menu(console).Run(_settings);

        Assert.True(File.Exists(path));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
        Assert.Contains("tomt eller Esc avbryter", console.Output);
    }

    [Fact]
    public void Esc_CancelsARename_AndANameWithBrackets_IsShownAsItIs()
    {
        string path = Stereo("tagning [1]", seconds: 1, written: DateTime.Now);
        TestConsole console = Console(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.Enter);
        console.Input.PushText("nytt");
        console.Input.PushKey(ConsoleKey.Escape);
        console.Input.PushKey(ConsoleKey.End);
        console.Input.PushKey(ConsoleKey.Enter);

        Menu(console).Run(_settings);

        Assert.True(File.Exists(path));
        Assert.Single(Directory.GetFiles(_folder));
        Assert.Contains("Nu heter den tagning [1].", console.Output);
    }

    [Fact]
    public void AFolderThatCannotBeRead_IsASwedishError()
    {
        Stereo("dold", seconds: 1, written: DateTime.Now);
        File.SetUnixFileMode(_folder, UnixFileMode.None);
        TestConsole console = Console();

        Menu(console).Run(_settings);

        Assert.Contains($"Inspelningsmappen {_folder} kunde inte läsas.", console.Output);
    }

    [Theory]
    [InlineData("", "tomt")]
    [InlineData("   ", "tomt")]
    [InlineData("a\\b", "filnamn")]
    [InlineData(".", "filnamn")]
    [InlineData("ANNAN", "redan")]
    public void NameProblems_AreSwedishMessages(string name, string says)
    {
        RecordingEntry entry = Entry(Stereo("gammal", seconds: 1, written: DateTime.Now));
        Stereo("annan", seconds: 1, written: DateTime.Now);

        Assert.Contains(says, RecordingLibrary.NameProblem(entry, name));
        Assert.Null(RecordingLibrary.NameProblem(entry, "Gammal"));
    }

    [Fact]
    public void AFileGoneSinceTheList_IsASwedishError_ForEachAction()
    {
        RecordingEntry entry = Entry(Stereo("borta", seconds: 1, written: DateTime.Now));
        File.Delete(entry.Path);
        TestConsole console = Console();

        Menu(console).Listen(entry, _settings);

        Assert.Contains(RecordingLibrary.MissingMessage(entry), console.Output);
        Assert.Equal(RecordingLibrary.MissingMessage(entry), RecordingLibrary.Rename(entry, "ny"));
        Assert.Equal(RecordingLibrary.MissingMessage(entry), RecordingLibrary.Delete(entry));
        Assert.Empty(_opened);
    }

    [Fact]
    public void AnUnreadableFile_CannotBeHeard_ButCanBeRenamedAndDeleted()
    {
        string path = Path.Combine(_folder, "trasig.wav");
        File.WriteAllText(path, "inte en WAV-fil");
        TestConsole console = Console();

        Menu(console).Listen(Entry(path), _settings);

        Assert.Contains($"Filen {path} kunde inte läsas", console.Output);
        Assert.Null(RecordingLibrary.Rename(Entry(path), "lagad"));
        Assert.Null(RecordingLibrary.Delete(Entry(Path.Combine(_folder, "lagad.wav"))));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void AFolderThatCannotBeWritten_GivesSwedishErrors_ForRenameAndDelete()
    {
        RecordingEntry entry = Entry(Stereo("låst", seconds: 1, written: DateTime.Now));
        File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        Assert.StartsWith("Inspelningen låst kunde inte byta namn", RecordingLibrary.Rename(entry, "ny"));
        Assert.StartsWith("Inspelningen låst kunde inte tas bort", RecordingLibrary.Delete(entry));
        Assert.True(File.Exists(entry.Path));
    }

    [Fact]
    public void ADeviceThatCannotPlay_IsASwedishError()
    {
        RecordingEntry entry = Entry(Stereo("spår", seconds: 1, written: DateTime.Now));
        TestConsole console = Console(ConsoleKey.Enter);
        RecordingsMenu menu = new(console, _ => throw new AudioDeviceException("Utgången stöder inte 48000 Hz."));

        menu.Listen(entry, _settings);

        Assert.Contains("Utgången stöder inte 48000 Hz.", console.Output);
    }

    private static TestConsole Console(params ConsoleKey[] keys)
    {
        TestConsole console = TestConsoles.Wide();
        foreach (ConsoleKey key in keys)
        {
            console.Input.PushKey(key);
        }

        return console;
    }

    // Once the playback has played to its end, Tillbaka in the recording's choices and then in the list; pressed
    // earlier, the player would take the keys.
    private static MemoryPlayback BackAfterPlaying(TestConsole console)
    {
        return new MemoryPlayback
        {
            WhenFinished = () =>
            {
                foreach (ConsoleKey key in new[] { ConsoleKey.End, ConsoleKey.Enter, ConsoleKey.End, ConsoleKey.Enter })
                {
                    console.Input.PushKey(key);
                }
            },
        };
    }

    private static RecordingEntry Entry(string path)
    {
        return RecordingLibrary.List(Path.GetDirectoryName(path)!).Single(entry => entry.Path == path);
    }

    private RecordingsMenu Menu(TestConsole console)
    {
        return new RecordingsMenu(
            console,
            settings =>
            {
                _opened.Add(settings);
                return _playback;
            },
            pollInterval: TimeSpan.Zero);
    }

    private string Stereo(string name, int seconds, DateTime written)
    {
        string path = WriteStereo(name, left: 0.25f, right: -0.5f, frames: seconds * SampleRate, sampleRate: SampleRate);
        File.SetLastWriteTime(path, written);
        return path;
    }

    private string WriteStereo(string name, float left, float right, int frames, int sampleRate)
    {
        float[] interleaved = new float[frames * 2];
        for (int frame = 0; frame < frames; frame++)
        {
            interleaved[2 * frame] = left;
            interleaved[(2 * frame) + 1] = right;
        }

        string path = Path.Combine(_folder, name + RecordingLibrary.Extension);
        File.WriteAllBytes(path, TestWav.Pcm16(sampleRate, 2, interleaved));
        return path;
    }
}
