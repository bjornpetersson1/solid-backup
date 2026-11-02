using SolidBackup.Recording;

namespace SolidBackup.Tests.Recording;

public sealed class RecordingFileTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 31, 14, 5, 12, DateTimeKind.Local);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests", Guid.NewGuid().ToString("N"), "recordings");

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);
    }

    [Fact]
    public void Create_MakesTheFolderAndNamesTheFileAfterTheStartTime()
    {
        (string path, FileStream stream) = RecordingFile.Create(_folder, Start);
        stream.Dispose();

        Assert.Equal(Path.Combine(_folder, "2026-10-31_14-05-12.wav"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Create_WhenTheNameIsTaken_AddsANumber()
    {
        RecordingFile.Create(_folder, Start).Stream.Dispose();
        RecordingFile.Create(_folder, Start).Stream.Dispose();

        (string third, FileStream stream) = RecordingFile.Create(_folder, Start);
        stream.Dispose();

        Assert.Equal(Path.Combine(_folder, "2026-10-31_14-05-12_3.wav"), third);
    }

    [Fact]
    public void Create_InAFolderThatIsAFile_Throws()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_folder)!);
        File.WriteAllText(_folder, "");

        Assert.ThrowsAny<IOException>(() => RecordingFile.Create(_folder, Start));
    }
}
