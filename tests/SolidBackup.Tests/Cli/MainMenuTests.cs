using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Tests.Audio.Devices;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public sealed class MainMenuTests : IDisposable
{
    // Where each item is in the menu.
    private static readonly int Start = Item(MainMenu.StartChoice);
    private static readonly int Input = Item("Ingång");
    private static readonly int Output = Item("Utgång");
    private static readonly int Key = Item("Tonart");
    private static readonly int Style = Item("Stil");
    private static readonly int Sound = Item("Ljud");
    private static readonly int Rhythm = Item("Rytm");
    private static readonly int Features = Item("Nivåfunktioner");
    private static readonly int Recording = Item("Inspelning");
    private static readonly int Quit = Item(MainMenu.QuitChoice);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests", Guid.NewGuid().ToString("N"));
    private readonly List<(AppSettings Settings, bool Record)> _started = [];

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Quit_ShowsTitleTableAndChoicesAndReturnsZero()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Quit);

        int exitCode = Menu(console).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains(MainMenu.Title, console.Output);
        Assert.Contains(MainMenu.StartChoice, console.Output);
        Assert.Contains(MainMenu.QuitChoice, console.Output);
        Assert.Contains("Kyrko", console.Output);
        Assert.Contains("Held, 90 BPM, 4/4", console.Output);
        Assert.Contains("grind, ansättningar, ackordkaraktär, dynamik", console.Output);
        Assert.Empty(_started);
    }

    [Fact]
    public void StartIsPreselected_AndTheMenuComesBackAfterLiveMode()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console, Quit);

        int exitCode = Menu(console).Run();

        Assert.Equal(0, exitCode);
        (AppSettings settings, bool record) = Assert.Single(_started);
        Assert.Equal(TestSettings.Default, settings);
        Assert.False(record);
    }

    [Fact]
    public void EveryChoice_ReachesLiveMode()
    {
        TestConsole console = TestConsoles.Wide();
        ChangeEverything(console);
        Choose(console, Start);
        Choose(console, Quit);

        Menu(console).Run();

        (AppSettings settings, bool record) = Assert.Single(_started);
        Assert.Equal(Changed, settings);
        Assert.True(record);
    }

    [Fact]
    public void TheChoices_AreRememberedByTheNextMenu()
    {
        TestConsole first = TestConsoles.Wide();
        ChangeEverything(first);
        Choose(first, Quit);
        Menu(first).Run();

        // The next start shows the choices and preselects them: Enter on the style keeps Blues.
        TestConsole next = TestConsoles.Wide();
        Choose(next, Style);
        next.Input.PushKey(ConsoleKey.Enter);
        Choose(next, Start);
        Choose(next, Quit);
        Menu(next).Run();

        Assert.Contains("USB Audio", next.Output);
        Assert.Contains("D dorisk", next.Output);
        Assert.Contains("Blues", next.Output);
        Assert.Contains("Organ", next.Output);
        Assert.Contains("Strum, 120 BPM, 3/4", next.Output);
        Assert.Contains("ansättningar, ackordkaraktär, dynamik", next.Output);
        Assert.Equal(Changed, Assert.Single(_started).Settings);
    }

    [Fact]
    public void ATempoOutOfRange_IsAskedAgainInSwedish()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Rhythm);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("300");
        console.Input.PushTextWithEnter("100");
        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console, Start);
        Choose(console, Quit);

        Menu(console).Run();

        Assert.Contains(MainMenu.BpmRangeMessage, console.Output);
        Assert.Equal(100, Assert.Single(_started).Settings.Bpm);
    }

    [Fact]
    public void AutoKey_AsksForNoMode()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Key);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console); // Ionian
        Choose(console, Key);
        console.Input.PushKey(ConsoleKey.UpArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console, Start);
        Choose(console, Quit);

        Menu(console).Run();

        // Had the mode been asked after auto, it would have taken the Enter meant for Start.
        Assert.Equal("auto", Assert.Single(_started).Settings.Key);
    }

    [Fact]
    public void NoFeatures_IsAChoice()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Features);
        foreach (int _ in Enumerable.Range(0, 4))
        {
            console.Input.PushKey(ConsoleKey.Spacebar);
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console, Start);
        Choose(console, Quit);

        Menu(console).Run();

        AppSettings settings = Assert.Single(_started).Settings;
        Assert.False(settings.GateOn || settings.OnsetsOn || settings.ChordCharacterOn || settings.DynamicsOn);
        Assert.Contains("inga", console.Output);
    }

    [Fact]
    public void BuiltInSpeakers_AreWarnedAbout()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Quit);
        Store().Save(TestSettings.Default with { OutputDevice = TestDevices.Speakers.Name });

        Menu(console, preflight: TestPreflight.Create(routes: new FakeRoutes(TestPreflight.Speakers))).Run();

        Assert.Contains(AudioPreflight.SpeakerWarning, console.Output);
    }

    [Fact]
    public void ASavedDeviceThatIsGone_IsShownAsMissing()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Quit);
        Store().Save(TestSettings.Default with { InputDevice = "Gammal mikrofon" });

        Menu(console).Run();

        Assert.Contains("Gammal mikrofon (saknas)", console.Output);
    }

    [Fact]
    public void AChoiceThatCannotBeSaved_WarnsAndStillApplies()
    {
        // A file where the settings folder should be, so the folder cannot be created.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "nested"), "");
        TestConsole console = TestConsoles.Wide();
        Choose(console, Sound);
        Choose(console, 2); // Pad, Organ, Pluck
        Choose(console, Start);
        Choose(console, Quit);

        Menu(console).Run();

        Assert.Contains("Valet kunde inte sparas", console.Output);
        Assert.Equal("Pluck", Assert.Single(_started).Settings.Sound);
    }

    [Fact]
    public void DevicesThatCannotBeListed_LeaveTheMenuWorking()
    {
        TestConsole console = TestConsoles.Wide();
        Choose(console, Quit);

        int exitCode = new MainMenu(console, Store(), () => throw new AudioDeviceException("PortAudio kunde inte läsa enheterna."), TestPreflight.Create(), StartLive).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Ljudfel: PortAudio kunde inte läsa enheterna.", console.Output);
    }

    [Fact]
    public void CtrlCInLiveMode_QuitsTheAppOnceLiveModeHasClosed()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Enter);

        // Live mode returns once its audio is closed, saying Ctrl+C stopped it; no Avsluta is needed.
        int exitCode = new MainMenu(console, Store(), () => new FakeCatalog(), TestPreflight.Create(), (_, _) => new LiveExit(0, Interrupted: true)).Run();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void CtrlCInLiveMode_KeepsLiveModesExitCode()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Enter);

        // A recording that could not be saved is not reported as success.
        int exitCode = new MainMenu(console, Store(), () => new FakeCatalog(), TestPreflight.Create(), (_, _) => new LiveExit(1, Interrupted: true)).Run();

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void ADefaultOutputThatBecameTheSpeakersDuringLiveMode_IsWarnedAboutAfterwards()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console, Quit);
        AudioDeviceInfo speakersAsDefault = TestDevices.Speakers with { IsDefaultOutput = true };
        AudioDeviceInfo[] devices = [TestDevices.Microphone, TestDevices.Headphones];
        AudioPreflight preflight = TestPreflight.Create(
            routes: new FakeRoutes(name => name == TestDevices.Speakers.Name ? TestPreflight.Speakers : TestPreflight.HeadphoneJack));

        new MainMenu(console, Store(), () => new FakeCatalog(devices), preflight, (_, _) =>
        {
            // The headphones are unplugged while playing.
            devices = [TestDevices.Microphone, speakersAsDefault];
            return new LiveExit(0, Interrupted: false);
        }).Run();

        // Not before live mode, with the headphones; once after it.
        Assert.Single(console.Output.Split(AudioPreflight.SpeakerWarning).Skip(1));
    }

    [Fact]
    public void ASavedDeviceNameInAnotherCase_StartsOnItsDevice()
    {
        TestConsole console = TestConsoles.Wide();
        Store().Save(TestSettings.Default with { InputDevice = "usb audio" });
        Choose(console, Input);
        Choose(console);
        Choose(console, Start);
        Choose(console, Quit);

        Menu(console).Run();

        Assert.Equal(TestDevices.UsbInterface.Name, Assert.Single(_started).Settings.InputDevice);
        Assert.DoesNotContain("(saknas)", console.Output);
    }

    [Fact]
    public void NonInteractiveTerminal_ShowsSwedishMessageAndReturnsOne()
    {
        TestConsole console = new();

        int exitCode = Menu(console).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains(MainMenu.NotInteractiveMessage, console.Output);
    }

    private static AppSettings Changed => TestSettings.Default with
    {
        InputDevice = TestDevices.UsbInterface.Name,
        OutputDevice = TestDevices.Headphones.Name,
        Key = "D",
        Mode = "Dorian",
        HarmonyStyle = "Blues",
        Sound = "Organ",
        RhythmPattern = "Strum",
        Bpm = 120,
        TimeSignature = "3/4",
        GateOn = false,
        RecordOn = true,
    };

    // Each prompt starts on the current value, so every step moves from the defaults.
    private static void ChangeEverything(TestConsole console)
    {
        Choose(console, Input);
        Choose(console, 2); // "", Microphone, USB Audio
        Choose(console, Output);
        Choose(console, 2); // "", USB Audio, Externa hörlurar, Högtalare
        Choose(console, Key);
        Choose(console, 3); // auto, C, Db, D
        Choose(console, 1); // Ionian, Dorian
        Choose(console, Style);
        Choose(console, 5); // Church, Campfire, Drone, Pop, Modal, Blues
        Choose(console, Sound);
        Choose(console, 1); // Pad, Organ
        Choose(console, Rhythm);
        Choose(console, 2); // Held, Arpeggio, Strum
        console.Input.PushTextWithEnter("120");
        Choose(console, 1); // 4/4, 3/4
        Choose(console, Features);
        console.Input.PushKey(ConsoleKey.Spacebar); // gate off
        console.Input.PushKey(ConsoleKey.Enter);
        Choose(console, Recording);
        console.Input.PushKey(ConsoleKey.UpArrow); // på, [av]
        console.Input.PushKey(ConsoleKey.Enter);
    }

    // Moves down from where the prompt starts and picks that item.
    private static void Choose(TestConsole console, int down = 0)
    {
        foreach (int _ in Enumerable.Range(0, down))
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
    }

    private SettingsStore Store()
    {
        return new SettingsStore(SettingsStore.BundledDefaultsPath, Path.Combine(_folder, "nested", "usersettings.json"));
    }

    private MainMenu Menu(TestConsole console, AudioPreflight? preflight = null)
    {
        return new MainMenu(console, Store(), () => new FakeCatalog(), preflight ?? TestPreflight.Create(), StartLive);
    }

    private static int Item(string choice)
    {
        return MainMenu.Choices.ToList().IndexOf(choice);
    }

    private LiveExit StartLive(AppSettings settings, bool record)
    {
        _started.Add((settings, record));
        return new LiveExit(0, Interrupted: false);
    }
}
