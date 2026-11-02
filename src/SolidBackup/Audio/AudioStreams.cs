namespace SolidBackup.Audio;

/// <summary>How an input and an output start and stop together, for the pipeline and for live mode.</summary>
public static class AudioStreams
{
    /// <summary>
    /// Starts the output first, so the input's first buffer has somewhere to go, then the input; if the input fails
    /// to start, the output is stopped again.
    /// </summary>
    /// <param name="renderer">Fills the output's buffers; without one, the output plays what is written to it.</param>
    public static void Start(IAudioInput input, IAudioInputHandler handler, IAudioOutput output, IAudioRenderer? renderer = null)
    {
        output.Start(renderer);
        try
        {
            input.Start(handler);
        }
        catch
        {
            output.Stop();
            throw;
        }
    }

    /// <summary>Stops the output first, so it never plays the gap left by an input that has already stopped.</summary>
    public static void Stop(IAudioInput input, IAudioOutput output)
    {
        try
        {
            output.Stop();
        }
        finally
        {
            input.Stop();
        }
    }
}
