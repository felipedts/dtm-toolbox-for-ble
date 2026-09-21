using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DtmToolbox.Protocol;

namespace DtmToolbox.Controls;

/// <summary>
/// Bar chart with one column per RF channel, in frequency order. The top axis shows the link
/// layer channel index and the bottom axis the frequency. A transmitter chart plots the
/// transmit power in dBm of the active channel; a receiver chart plots packets received.
/// </summary>
public sealed class ChannelChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(double[]), typeof(ChannelChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsReceiverProperty = DependencyProperty.Register(
        nameof(IsReceiver), typeof(bool), typeof(ChannelChart), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumDbmProperty = DependencyProperty.Register(
        nameof(MinimumDbm), typeof(double), typeof(ChannelChart), new FrameworkPropertyMetadata(-40.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumDbmProperty = DependencyProperty.Register(
        nameof(MaximumDbm), typeof(double), typeof(ChannelChart), new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double LeftMargin = 64;
    private const double RightMargin = 16;
    private const double TopMargin = 58;
    private const double BottomMargin = 80;
    private const double BarWidthRatio = 0.72;
    private const double TickFontSize = 11;
    private const double TitleFontSize = 14;
    private const double ValueFontSize = 9;
    private const double DbmHeadroomBelow = 5;
    private const double DbmHeadroomAbove = 4;
    private const int MinimumPacketScale = 10;

    private static readonly Typeface ChartTypeface = new Typeface("Segoe UI");
    private static readonly Brush ColumnBrush = Frozen(Color.FromRgb(0xEC, 0xEF, 0xF1));
    private static readonly Brush BarBrush = Frozen(Color.FromRgb(0x00, 0xAC, 0xC1));
    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0x90, 0xA4, 0xAE));

    /// <summary>One value per RF channel. NaN draws no bar.</summary>
    public double[]? Values
    {
        get => (double[]?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public bool IsReceiver
    {
        get => (bool)GetValue(IsReceiverProperty);
        set => SetValue(IsReceiverProperty, value);
    }

    /// <summary>Lowest transmit power on the dBm axis.</summary>
    public double MinimumDbm
    {
        get => (double)GetValue(MinimumDbmProperty);
        set => SetValue(MinimumDbmProperty, value);
    }

    /// <summary>Highest transmit power on the dBm axis.</summary>
    public double MaximumDbm
    {
        get => (double)GetValue(MaximumDbmProperty);
        set => SetValue(MaximumDbmProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var plot = new Rect(LeftMargin, TopMargin, ActualWidth - LeftMargin - RightMargin, ActualHeight - TopMargin - BottomMargin);
        if (plot.Width < DtmChannel.Count * 4 || plot.Height < 40)
        {
            return;
        }

        double[] values = Values ?? new double[0];
        double axisMinimum;
        double axisMaximum;
        double tickStep;
        if (IsReceiver)
        {
            axisMinimum = 0;
            axisMaximum = PacketScale(values);
            tickStep = axisMaximum / 5;
        }
        else
        {
            axisMinimum = MinimumDbm - DbmHeadroomBelow;
            axisMaximum = MaximumDbm + DbmHeadroomAbove;
            tickStep = 10;
        }

        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double slot = plot.Width / DtmChannel.Count;
        double barWidth = Math.Max(2, Math.Floor(slot * BarWidthRatio));

        DrawCentered(drawingContext, Text("Bluetooth LE channel", TitleFontSize, LabelBrush, pixelsPerDip), plot.Left + (plot.Width / 2), 6);
        DrawCentered(drawingContext, Text("MHz", TitleFontSize, LabelBrush, pixelsPerDip), plot.Left + (plot.Width / 2), ActualHeight - 24);
        DrawRotated(drawingContext, Text(IsReceiver ? "Received packets" : "Strength (dBm)", TitleFontSize, LabelBrush, pixelsPerDip), 16, plot.Top + (plot.Height / 2));

        double firstTick = FirstTick(axisMinimum, tickStep, IsReceiver);
        double lastTick = IsReceiver ? axisMaximum : MaximumDbm;
        for (int i = 0; firstTick + (i * tickStep) <= lastTick + 0.0001; i++)
        {
            double tick = firstTick + (i * tickStep);
            double y = plot.Bottom - ((tick - axisMinimum) / (axisMaximum - axisMinimum) * plot.Height);
            FormattedText label = Text(tick.ToString("0", CultureInfo.InvariantCulture), TickFontSize, LabelBrush, pixelsPerDip);
            drawingContext.DrawText(label, new Point(plot.Left - 12 - label.Width, y - (label.Height / 2)));
        }

        for (int channel = 0; channel < DtmChannel.Count; channel++)
        {
            double center = plot.Left + (slot * channel) + (slot / 2);
            double x = Math.Round(center - (barWidth / 2));
            drawingContext.DrawRectangle(ColumnBrush, null, new Rect(x, plot.Top, barWidth, plot.Height));

            string index = DtmChannel.ToLinkLayerIndex(channel).ToString("00", CultureInfo.InvariantCulture);
            DrawCentered(drawingContext, Text(index, TickFontSize, LabelBrush, pixelsPerDip), center, plot.Top - 24);

            string frequency = DtmChannel.FrequencyMhz(channel).ToString(CultureInfo.InvariantCulture);
            DrawRotated(drawingContext, Text(frequency, TickFontSize, LabelBrush, pixelsPerDip), center, plot.Bottom + 24);

            double value = channel < values.Length ? values[channel] : double.NaN;
            if (double.IsNaN(value) || (IsReceiver && value <= 0))
            {
                continue;
            }

            double clamped = Math.Max(axisMinimum, Math.Min(axisMaximum, value));
            double barHeight = Math.Max(1, (clamped - axisMinimum) / (axisMaximum - axisMinimum) * plot.Height);
            drawingContext.DrawRectangle(BarBrush, null, new Rect(x, plot.Bottom - barHeight, barWidth, barHeight));

            FormattedText valueLabel = Text(value.ToString("0", CultureInfo.InvariantCulture), ValueFontSize, BarBrush, pixelsPerDip);
            double labelY = plot.Bottom - barHeight - 2;
            if (valueLabel.Width <= slot + 6)
            {
                DrawCentered(drawingContext, valueLabel, center, labelY - valueLabel.Height);
            }
            else
            {
                DrawRotated(drawingContext, valueLabel, center, labelY - (valueLabel.Width / 2));
            }
        }
    }

    // Top of the packet axis: the smallest of 1, 2, 5 times a power of ten that holds the largest count.
    private static double PacketScale(double[] values)
    {
        double largest = MinimumPacketScale;
        foreach (double value in values)
        {
            if (!double.IsNaN(value) && value > largest)
            {
                largest = value;
            }
        }

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(largest)));
        foreach (double factor in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            if (largest <= factor * magnitude)
            {
                return factor * magnitude;
            }
        }

        return 10 * magnitude;
    }

    private static double FirstTick(double axisMinimum, double step, bool fromZero) =>
        fromZero ? 0 : Math.Ceiling(axisMinimum / step) * step;

    private static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ChartTypeface, size, brush, pixelsPerDip);

    private static void DrawCentered(DrawingContext drawingContext, FormattedText text, double centerX, double top) =>
        drawingContext.DrawText(text, new Point(centerX - (text.Width / 2), top));

    // Draws text turned 90 degrees counterclockwise, centered on the given point.
    private static void DrawRotated(DrawingContext drawingContext, FormattedText text, double centerX, double centerY)
    {
        drawingContext.PushTransform(new RotateTransform(-90, centerX, centerY));
        drawingContext.DrawText(text, new Point(centerX - (text.Width / 2), centerY - (text.Height / 2)));
        drawingContext.Pop();
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
