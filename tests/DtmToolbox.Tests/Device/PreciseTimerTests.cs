using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DtmToolbox.Device;
using Xunit;

namespace DtmToolbox.Tests.Device;

public class PreciseTimerTests
{
    // A loaded build machine can delay a thread at any moment, so the bounds look at the median.
    private const double ToleranceMs = 3.0;

    [Theory]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(30)]
    public void Wait_TakesTheRequestedTime(int milliseconds)
    {
        using var timer = new PreciseTimer();
        var measured = new List<double>();
        for (int i = 0; i < 15; i++)
        {
            var clock = Stopwatch.StartNew();
            timer.Wait(milliseconds, CancellationToken.None);
            measured.Add(clock.Elapsed.TotalMilliseconds);
        }

        Assert.All(measured, elapsed => Assert.True(elapsed >= milliseconds - 0.1, "A wait ended early: " + elapsed + " ms"));
        Assert.InRange(Median(measured), milliseconds, milliseconds + ToleranceMs);
    }

    [Fact]
    public void Wait_EndsWhenTheTokenIsCancelled()
    {
        using var timer = new PreciseTimer();
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(50);

        var clock = Stopwatch.StartNew();
        timer.Wait(5000, cancel.Token);

        Assert.InRange(clock.ElapsedMilliseconds, 30, 1000);
    }

    [Fact]
    public async Task Sweep_KeepsTheTimePerChannel()
    {
        const int DwellMs = 20;
        var progress = new TimingProgress();
        using var stop = new CancellationTokenSource();
        stop.CancelAfter(600);
        var runner = new TestRunner(new DtmDevice(new FakeLink()));
        var plan = new TestPlan { Mode = TestMode.Receiver, FirstChannel = 0, LastChannel = 39, DwellTimeMs = DwellMs };

        await runner.RunAsync(plan, progress, stop.Token);

        // The last channel is cut short by the stop request.
        List<double> onTimes = progress.OnTimes().Take(Math.Max(0, progress.OnTimes().Count - 1)).ToList();
        Assert.True(onTimes.Count >= 10, "Too few channels in 600 ms: " + onTimes.Count);
        Assert.InRange(Median(onTimes), DwellMs, DwellMs + ToleranceMs);
    }

    private static double Median(List<double> values)
    {
        List<double> sorted = values.OrderBy(value => value).ToList();
        return sorted[sorted.Count / 2];
    }

    private sealed class TimingProgress : IProgress<TestProgress>
    {
        private readonly object _sync = new object();
        private readonly List<long> _started = new List<long>();
        private readonly List<long> _ended = new List<long>();

        public void Report(TestProgress value)
        {
            long now = Stopwatch.GetTimestamp();
            lock (_sync)
            {
                if (value.Kind == TestProgressKind.ChannelStarted)
                {
                    _started.Add(now);
                }
                else if (value.Kind == TestProgressKind.ChannelEnded)
                {
                    _ended.Add(now);
                }
            }
        }

        public List<double> OnTimes()
        {
            lock (_sync)
            {
                return _ended.Select((end, index) => (end - _started[index]) * 1000.0 / Stopwatch.Frequency).ToList();
            }
        }
    }
}
