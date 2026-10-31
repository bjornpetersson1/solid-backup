namespace SolidBackup.Audio.Devices;

/// <summary>One audio device as the audio system reports it.</summary>
public sealed record AudioDeviceInfo(
    int Index,
    string Name,
    int InputChannels,
    int OutputChannels,
    double LowInputLatency,
    double LowOutputLatency,
    bool IsDefaultInput,
    bool IsDefaultOutput)
{
    public int Channels(AudioDirection direction)
    {
        return direction == AudioDirection.Input ? InputChannels : OutputChannels;
    }

    public double LowLatency(AudioDirection direction)
    {
        return direction == AudioDirection.Input ? LowInputLatency : LowOutputLatency;
    }

    public bool Supports(AudioDirection direction)
    {
        return Channels(direction) > 0;
    }

    public bool IsDefault(AudioDirection direction)
    {
        return direction == AudioDirection.Input ? IsDefaultInput : IsDefaultOutput;
    }
}
