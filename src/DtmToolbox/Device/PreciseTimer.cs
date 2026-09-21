using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DtmToolbox.Device;

/// <summary>
/// Waits that keep to about a millisecond. Windows wakes a sleeping thread on a timer tick,
/// 15.6 ms apart by default, which turns a 20 ms wait into 31 ms. While an instance exists it
/// asks Windows for a 1 ms tick, and each wait sleeps through most of its time and spins
/// through the last stretch. How long a 1 ms sleep really takes is measured as the waits go,
/// so a system that does not grant the 1 ms tick (Windows 11 may not, for a minimized window)
/// still gets the right duration, at the cost of spinning for longer.
/// </summary>
public sealed class PreciseTimer : IDisposable
{
    private const uint TickMs = 1;
    private const double InitialSleepCostMs = 2.0;
    private const double MarginMs = 0.5;

    private readonly bool _tickRequested;
    private double _sleepCostMs = InitialSleepCostMs;

    public PreciseTimer()
    {
        _tickRequested = timeBeginPeriod(TickMs) == 0;
    }

    /// <summary>Blocks the calling thread for the given time, or until the token is cancelled.</summary>
    public void Wait(int milliseconds, CancellationToken token)
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(milliseconds * (double)Stopwatch.Frequency / 1000);
        while (!token.IsCancellationRequested)
        {
            double remainingMs = ToMilliseconds(deadline - Stopwatch.GetTimestamp());
            if (remainingMs <= 0)
            {
                return;
            }

            if (remainingMs > _sleepCostMs + MarginMs)
            {
                long before = Stopwatch.GetTimestamp();
                token.WaitHandle.WaitOne(1);
                double cost = ToMilliseconds(Stopwatch.GetTimestamp() - before);

                // A longer sleep counts at once, a shorter one brings the estimate down slowly.
                _sleepCostMs = Math.Max(cost, (_sleepCostMs * 0.9) + (cost * 0.1));
            }
            else
            {
                Thread.SpinWait(20);
            }
        }
    }

    public void Dispose()
    {
        if (_tickRequested)
        {
            timeEndPeriod(TickMs);
        }
    }

    private static double ToMilliseconds(long stopwatchTicks) => stopwatchTicks * 1000.0 / Stopwatch.Frequency;

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);
}
