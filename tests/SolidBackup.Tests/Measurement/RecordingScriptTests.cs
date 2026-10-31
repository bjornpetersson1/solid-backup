using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class RecordingScriptTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

    public void Dispose()
    {
        File.Delete(_path);
    }

    [Fact]
    public void Script_IsReadInOrder()
    {
        File.WriteAllLines(_path, ["# header", "scale,voice,C3 D#3 Eb3", "", "room,silence,"]);

        IReadOnlyList<ScriptedClip> script = RecordingScript.Read(_path);

        Assert.Equal(2, script.Count);
        Assert.Equal(("scale", RecordingKind.Voice), (script[0].Name, script[0].Kind));
        Assert.Equal([48, 51, 51], script[0].Notes);
        Assert.Equal(("room", RecordingKind.Silence), (script[1].Name, script[1].Kind));
        Assert.Empty(script[1].Notes);
    }

    [Theory]
    [InlineData("scale,voice", "väntade <klipp>,<voice|whistle|silence|breath>,<toner>")]
    [InlineData("scale,hum,C3", "väntade <klipp>,<voice|whistle|silence|breath>,<toner>")]
    [InlineData("scale,voice,H3", "okänd ton 'H3'")]
    [InlineData("scale,whistle,", "toner saknas")]
    [InlineData("room,breath,C3", "toner för ett klipp utan ton")]
    public void InvalidLine_GivesSwedishError(string line, string problem)
    {
        File.WriteAllLines(_path, [line]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => RecordingScript.Read(_path));

        Assert.Equal($"Tonlistan {_path}, rad 1: {problem}.", error.Message);
    }
}
