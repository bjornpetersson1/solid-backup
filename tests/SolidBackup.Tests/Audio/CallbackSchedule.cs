namespace SolidBackup.Tests.Audio;

/// <summary>
/// The order in which an input's and an output's callbacks arrive when the two run as separate streams: each side has
/// its own clock, the output's callbacks come steadily, and the input's come in bursts (several back to back, at the
/// time of the last). Built from a seed, so a test sees the same order every time.
/// </summary>
public static class CallbackSchedule
{
    /// <summary>
    /// The callbacks in arrival order, true for the input's and false for the output's, covering
    /// <paramref name="outputBuffers"/> output buffers.
    /// </summary>
    /// <param name="inputSpeed">The input's clock relative to the output's, e.g. 1.001 for 0.1 % faster.</param>
    /// <param name="maxBurst">The most input callbacks delivered back to back.</param>
    /// <param name="outputFirst">The output's first burst comes before the input's first.</param>
    /// <param name="burstsFrom">
    /// Input callbacks that start a burst of <paramref name="maxBurst"/>, e.g. at onsets, where heavier analysis
    /// delays the input's thread and its next callbacks come bunched.
    /// </param>
    public static IEnumerable<bool> Irregular(
        int outputBuffers, double inputSpeed, int maxBurst, int seed, bool outputFirst = false, IReadOnlySet<int>? burstsFrom = null)
    {
        Random random = new(seed);
        List<(double Time, int Tiebreak, bool Input)> callbacks = [];
        // Half a buffer apart, so which side starts is decided by the argument, not by a tie.
        AddBursts(callbacks, random, outputBuffers, period: 1, start: outputFirst ? 0 : 0.5, maxBurst: 1, input: false, burstsFrom: null);
        int inputBuffers = (int)Math.Ceiling(outputBuffers * inputSpeed) + maxBurst;
        AddBursts(callbacks, random, inputBuffers, period: 1 / inputSpeed, start: outputFirst ? 0.5 : 0, maxBurst, input: true, burstsFrom);
        return callbacks.OrderBy(callback => callback.Time).ThenBy(callback => callback.Tiebreak).Select(callback => callback.Input).ToArray();
    }

    // Each burst of 1 to maxBurst callbacks arrives at once, when its last callback is due; one starts at each of burstsFrom.
    private static void AddBursts(
        List<(double, int, bool)> callbacks, Random random, int count, double period, double start, int maxBurst, bool input, IReadOnlySet<int>? burstsFrom)
    {
        int index = 0;
        while (index < count)
        {
            int burst = burstsFrom?.Contains(index) == true ? maxBurst : random.Next(1, maxBurst + 1);

            // A random burst ends before a forced one, so that one starts where it is asked to.
            for (int next = index + 1; next < index + burst; next++)
            {
                if (burstsFrom?.Contains(next) == true)
                {
                    burst = next - index;
                }
            }

            double due = start + ((index + burst - 1) * period);
            for (int i = 0; i < burst; i++)
            {
                callbacks.Add((due, random.Next(), input));
            }

            index += burst;
        }
    }
}
