namespace SolidBackup.Analysis;

/// <summary>
/// A noise gate with hysteresis. It opens as soon as the level reaches the threshold and closes only
/// once the level has stayed below the threshold minus the hysteresis for the hold time, so a level
/// hovering around the threshold does not make it chatter. The hold is timed from the first low reading,
/// so one low reading never closes the gate however long the buffers are. Pure and allocation-free.
/// </summary>
public sealed class NoiseGate(int sampleRate, double thresholdDbfs, double hysteresisDb, double holdMs)
{
    private readonly double _closeBelowDbfs = thresholdDbfs - hysteresisDb;
    private readonly long _holdSamples = Duration.Samples(holdMs, sampleRate);
    // −1 while the level is above the close threshold; otherwise the time since the first low reading.
    private long _belowSamples = -1;
    private bool _open;

    /// <summary>Takes the level after a buffer of <paramref name="bufferLength"/> samples and returns whether the gate is open.</summary>
    public bool Process(double levelDbfs, int bufferLength)
    {
        if (levelDbfs >= thresholdDbfs)
        {
            _open = true;
        }

        _belowSamples = levelDbfs >= _closeBelowDbfs ? -1 : _belowSamples < 0 ? 0 : _belowSamples + bufferLength;
        if (_open && _belowSamples >= _holdSamples)
        {
            _open = false;
        }

        return _open;
    }
}
