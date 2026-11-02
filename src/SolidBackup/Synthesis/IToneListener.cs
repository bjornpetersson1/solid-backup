namespace SolidBackup.Synthesis;

/// <summary>Hears each tone as it starts or is released, on the sample it happens; called on the audio thread.</summary>
public interface IToneListener
{
    void OnStart(long sample, int note, double strength);

    void OnRelease(long sample, int note);
}
