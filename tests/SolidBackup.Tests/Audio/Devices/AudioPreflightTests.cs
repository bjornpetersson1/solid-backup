using SolidBackup.Audio.Devices;

namespace SolidBackup.Tests.Audio.Devices;

public class AudioPreflightTests
{
    [Theory]
    [InlineData(MicrophonePermission.Denied)]
    [InlineData(MicrophonePermission.Restricted)]
    public void CheckMicrophone_WhenBlocked_ExplainsHowToGrantAccess(MicrophonePermission status)
    {
        AudioPreflight preflight = new(new FakePermission(status), new FakeRoutes(), "iTerm");

        Assert.Equal(
            "iTerm har inte tillgång till mikrofonen. Öppna Systeminställningar → Integritet och säkerhet → Mikrofon, "
            + "slå på iTerm och starta sedan om iTerm.",
            preflight.CheckMicrophone().Message);
        Assert.True(preflight.CheckMicrophone().Blocked);
    }

    [Theory]
    [InlineData(MicrophonePermission.Authorized)]
    [InlineData(MicrophonePermission.Unknown)]
    public void CheckMicrophone_WhenAllowedOrUnknown_IsClear(MicrophonePermission status)
    {
        AudioPreflight preflight = TestPreflight.Create(status, new FakeRoutes());

        Assert.Equal(MicrophoneCheck.Clear, preflight.CheckMicrophone());
    }

    [Fact]
    public void CheckMicrophone_WhenNotAskedYet_ContinuesWithANotice()
    {
        AudioPreflight preflight = TestPreflight.Create(MicrophonePermission.NotDetermined);

        Assert.Equal(new MicrophoneCheck(false, AudioPreflight.FirstUseNotice), preflight.CheckMicrophone());
    }

    [Fact]
    public void CheckMicrophone_UnknownTerminal_StartsTheSentenceWithACapital()
    {
        AudioPreflight preflight = new(new FakePermission(MicrophonePermission.Denied), new FakeRoutes(), AudioPreflight.UnknownTerminal);

        string? message = preflight.CheckMicrophone().Message;

        Assert.StartsWith("Terminalappen har inte tillgång till mikrofonen.", message);
        Assert.Contains("slå på terminalappen", message);
    }

    [Theory]
    [InlineData("Apple_Terminal", "Terminal")]
    [InlineData("iTerm.app", "iTerm")]
    [InlineData("vscode", "Visual Studio Code")]
    [InlineData("WezTerm", "terminalappen")]
    [InlineData(null, "terminalappen")]
    public void TerminalName_NamesKnownTerminals(string? termProgram, string expected)
    {
        Assert.Equal(expected, AudioPreflight.TerminalName(termProgram));
    }

    [Theory]
    [InlineData("bltn", "ispk", true)]
    [InlineData("bltn", "hdpn", false)]
    [InlineData("bltn", null, true)]
    [InlineData("usb ", "ispk", false)]
    [InlineData("blue", null, false)]
    public void IsBuiltInSpeakers_OnlyForBuiltInInternalSpeakers(string transport, string? source, bool expected)
    {
        AudioPreflight preflight = TestPreflight.Create(routes: new FakeRoutes(new OutputRoute(transport, source)));

        Assert.Equal(expected, preflight.IsBuiltInSpeakers("Utgång"));
    }

    [Fact]
    public void SpeakerProblem_WarnsOnlyForTheBuiltInSpeakers()
    {
        Assert.Equal(AudioPreflight.SpeakerWarning, TestPreflight.Create(routes: new FakeRoutes(TestPreflight.Speakers)).SpeakerProblem("Utgång"));
        Assert.Null(TestPreflight.Create().SpeakerProblem("Utgång"));
    }

    [Fact]
    public void IsBuiltInSpeakers_WhenTheRouteCannotBeRead_IsFalse()
    {
        AudioPreflight preflight = TestPreflight.Create(routes: new FakeRoutes());

        Assert.False(preflight.IsBuiltInSpeakers("Utgång"));
    }
}
