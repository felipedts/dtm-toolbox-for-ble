using System;

namespace DtmToolbox.Protocol;

/// <summary>
/// RF channels as the test commands number them: 0 to 39 in frequency order, 2402 MHz to
/// 2480 MHz in 2 MHz steps. The link layer numbers the same frequencies differently, with the
/// advertising channels 37, 38 and 39 at 2402, 2426 and 2480 MHz.
/// </summary>
public static class DtmChannel
{
    public const int Count = 40;
    public const int Min = 0;
    public const int Max = Count - 1;

    private const int BaseFrequencyMhz = 2402;
    private const int SpacingMhz = 2;

    public static int FrequencyMhz(int channel)
    {
        Check(channel);
        return BaseFrequencyMhz + (SpacingMhz * channel);
    }

    /// <summary>Link layer channel index of an RF channel.</summary>
    public static int ToLinkLayerIndex(int channel)
    {
        Check(channel);
        switch (channel)
        {
            case 0:
                return 37;
            case 12:
                return 38;
            case 39:
                return 39;
            default:
                return channel < 12 ? channel - 1 : channel - 2;
        }
    }

    /// <summary>RF channel of a link layer channel index.</summary>
    public static int FromLinkLayerIndex(int index)
    {
        if (index < 0 || index > 39)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Link layer channel index must be between 0 and 39.");
        }

        switch (index)
        {
            case 37:
                return 0;
            case 38:
                return 12;
            case 39:
                return 39;
            default:
                return index <= 10 ? index + 1 : index + 2;
        }
    }

    internal static void Check(int channel)
    {
        if (channel < Min || channel > Max)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "RF channel must be between 0 and 39.");
        }
    }
}
