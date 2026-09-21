using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DtmToolbox.Device;
using DtmToolbox.Protocol;
using Xunit;

namespace DtmToolbox.Tests.Device;

/// <summary>
/// Transmit power on firmware without the setup command 0x09: the Nordic vendor command takes
/// over, with the nearest level the radio accepts.
/// </summary>
public class TransmitPowerFallbackTests
{
    private static readonly int[] RadioLevels = { -40, -20, -16, -12, -8, -4, 0, 2, 3, 4, 5, 6, 7, 8 };

    [Fact]
    public async Task RejectedSetupCommand_IsFollowedByTheVendorCommandWithTheSameLevel()
    {
        FakeLink link = LegacyFirmware();

        TestResult result = await Run(link, new TestPlan { TransmitPowerDbm = 4, VendorProfile = VendorProfile.NordicNrf5x });

        Assert.Equal(new[] { 0x0000, 0x0904, 0x840B }, link.CommandValues.Take(3));
        Assert.Equal(4, result.Power!.Value.LevelDbm);
        Assert.True(result.Power.Value.FromVendorCommand);
    }

    [Theory]
    [InlineData(-3, -4)]
    [InlineData(1, 0)]
    [InlineData(-30, -40)]
    [InlineData(-10, -12)]
    [InlineData(15, 8)]
    [InlineData(20, 8)]
    public async Task LevelTheRadioDoesNotHave_MovesToTheNearestOne_LowerFirst(int requested, int expected)
    {
        FakeLink link = LegacyFirmware();

        TestResult result = await Run(link, new TestPlan { TransmitPowerDbm = requested, VendorProfile = VendorProfile.NordicNrf5x });

        Assert.Equal(expected, result.Power!.Value.LevelDbm);
    }

    [Fact]
    public async Task AcceptedSetupCommand_NeverReachesTheVendorCommand()
    {
        var link = new FakeLink { TransmitPowerResponse = new DtmFrame(0x0008) };

        TestResult result = await Run(link, new TestPlan { TransmitPowerDbm = 4, VendorProfile = VendorProfile.NordicNrf5x });

        Assert.DoesNotContain(link.Commands, IsVendorPower);
        Assert.False(result.Power!.Value.FromVendorCommand);
    }

    [Fact]
    public async Task GenericProfile_DoesNotTryTheVendorCommand()
    {
        FakeLink link = LegacyFirmware();
        var plan = new TestPlan { TransmitPowerDbm = 4, VendorProfile = VendorProfile.Generic };

        var exception = await Assert.ThrowsAsync<DtmCommandRejectedException>(() => Run(link, plan));

        Assert.Equal(DtmCommand.SetTransmitPower(4), exception.Command);
        Assert.DoesNotContain(link.Commands, IsVendorPower);
    }

    [Fact]
    public async Task DeviceThatAcceptsNeitherCommand_FailsTheRun()
    {
        var link = new FakeLink { RejectWhen = command => IsSetupPower(command) || IsVendorPower(command) };
        var plan = new TestPlan { TransmitPowerDbm = 0, VendorProfile = VendorProfile.NordicNrf5x };

        DtmException exception = await Assert.ThrowsAsync<DtmException>(() => Run(link, plan));

        Assert.Contains("0x09", exception.Message);
        Assert.DoesNotContain(0xC000, link.CommandValues);
    }

    [Theory]
    [InlineData(false, 3, 3, false)]
    [InlineData(true, 3, 3, true)]
    [InlineData(false, -30, -40, false)]
    [InlineData(true, -30, -40, true)]
    public async Task SimulatedDevice_AppliesThePowerThroughTheCommandItsFirmwareHas(bool legacyFirmware, int requested, int expected, bool viaVendor)
    {
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(new SimulatedDtmLink(legacyFirmware)), StopAtOnce(stop));
        var plan = new TestPlan { TransmitPowerDbm = requested, VendorProfile = VendorProfile.NordicNrf5x };

        TestResult result = await runner.RunAsync(plan, null, stop.Token);

        Assert.Equal(expected, result.Power!.Value.LevelDbm);
        Assert.Equal(viaVendor, result.Power.Value.FromVendorCommand);
    }

    [Fact]
    public async Task SimulatedDevice_CountsPacketsOnAReceiverSweep()
    {
        int waits = 0;
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(new SimulatedDtmLink(legacyFirmware: false)), async (milliseconds, token) =>
        {
            await Task.Delay(20);
            if (Interlocked.Increment(ref waits) >= 3)
            {
                stop.Cancel();
            }
        });
        var plan = new TestPlan { Mode = TestMode.Receiver, FirstChannel = 0, LastChannel = 2, DwellTimeMs = 20 };

        TestResult result = await runner.RunAsync(plan, null, stop.Token);

        Assert.All(result.PacketsPerChannel.Take(3), count => Assert.True(count > 0));
        Assert.All(result.PacketsPerChannel.Skip(3), count => Assert.Equal(0, count));
    }

    private static FakeLink LegacyFirmware() => new FakeLink
    {
        RejectWhen = command => IsSetupPower(command) || (IsVendorPower(command) && !RadioLevels.Contains(VendorLevel(command))),
    };

    private static bool IsSetupPower(DtmFrame command) => command.High == (byte)SetupControl.SetTransmitPower;

    // Transmitter test command, vendor packet type, vendor command 2.
    private static bool IsVendorPower(DtmFrame command) => (command.Value & 0xC000) == 0x8000 && command.Low == 0x0B;

    private static int VendorLevel(DtmFrame command)
    {
        int field = command.High & 0x3F;
        return (field & 0x30) != 0 ? unchecked((sbyte)(field | 0xC0)) : field;
    }

    private static Task<TestResult> Run(FakeLink link, TestPlan plan)
    {
        var stop = new CancellationTokenSource();
        return new TestRunner(new DtmDevice(link), StopAtOnce(stop)).RunAsync(plan, null, stop.Token);
    }

    private static Func<int, CancellationToken, Task> StopAtOnce(CancellationTokenSource stop) => (milliseconds, token) =>
    {
        stop.Cancel();
        return Task.CompletedTask;
    };
}
