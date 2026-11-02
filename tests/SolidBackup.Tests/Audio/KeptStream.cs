namespace SolidBackup.Tests.Audio;

/// <summary>A memory stream whose contents can still be read after whoever wrote it has closed it.</summary>
internal sealed class KeptStream : MemoryStream
{
    public byte[] Contents { get; private set; } = [];

    protected override void Dispose(bool disposing)
    {
        Contents = ToArray();
        base.Dispose(disposing);
    }
}
