using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DtmToolbox.Controls;

/// <summary>
/// Slider drawn as a thin track with round thumbs. With <see cref="IsRange"/> it has two thumbs
/// and selects an interval. Values snap to whole numbers. A logarithmic scale gives the low end
/// of a wide range the same room as the high end.
/// </summary>
public sealed class TrackSlider : FrameworkElement
{
    public static readonly DependencyProperty MinimumProperty = Register(nameof(Minimum), 0.0);
    public static readonly DependencyProperty MaximumProperty = Register(nameof(Maximum), 100.0);
    public static readonly DependencyProperty ValueProperty = RegisterValue(nameof(Value));
    public static readonly DependencyProperty UpperValueProperty = RegisterValue(nameof(UpperValue));
    public static readonly DependencyProperty IsRangeProperty = Register(nameof(IsRange), false);
    public static readonly DependencyProperty IsLogarithmicProperty = Register(nameof(IsLogarithmic), false);

    private const double ThumbRadius = 6;
    private const double TrackThickness = 2;
    private const double ControlHeight = 20;

    private static readonly Brush TrackBrush = Frozen(Color.FromRgb(0xB0, 0xBE, 0xC5));
    private static readonly Brush ActiveBrush = Frozen(Color.FromRgb(0x45, 0x5A, 0x64));
    private static readonly Brush DisabledBrush = Frozen(Color.FromRgb(0x90, 0xA4, 0xAE));

    private bool _draggingUpper;

    public TrackSlider()
    {
        Focusable = true;
        Height = ControlHeight;
        Cursor = Cursors.Hand;
        IsEnabledChanged += (sender, e) => InvalidateVisual();
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The value, or the lower end of the interval when <see cref="IsRange"/> is set.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Upper end of the interval. Used only when <see cref="IsRange"/> is set.</summary>
    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public bool IsRange
    {
        get => (bool)GetValue(IsRangeProperty);
        set => SetValue(IsRangeProperty, value);
    }

    public bool IsLogarithmic
    {
        get => (bool)GetValue(IsLogarithmicProperty);
        set => SetValue(IsLogarithmicProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double width = ActualWidth;
        if (width <= 2 * ThumbRadius)
        {
            return;
        }

        double y = ActualHeight / 2;
        Brush active = IsEnabled ? ActiveBrush : DisabledBrush;

        // A transparent background makes the whole area take mouse input, not only the drawn parts.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, ActualHeight));
        drawingContext.DrawRectangle(TrackBrush, null, new Rect(ThumbRadius, y - (TrackThickness / 2), width - (2 * ThumbRadius), TrackThickness));

        double lower = ToPosition(Value);
        double from = IsRange ? lower : ThumbRadius;
        double to = IsRange ? ToPosition(UpperValue) : lower;
        drawingContext.DrawRectangle(active, null, new Rect(from, y - (TrackThickness / 2), Math.Max(0, to - from), TrackThickness));

        drawingContext.DrawEllipse(active, null, new Point(lower, y), ThumbRadius, ThumbRadius);
        if (IsRange)
        {
            drawingContext.DrawEllipse(active, null, new Point(to, y), ThumbRadius, ThumbRadius);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        double x = e.GetPosition(this).X;
        _draggingUpper = IsRange && Math.Abs(x - ToPosition(UpperValue)) < Math.Abs(x - ToPosition(Value));
        if (IsRange && Math.Abs(x - ToPosition(UpperValue)) == Math.Abs(x - ToPosition(Value)))
        {
            // Both thumbs on the same spot: the side of the click says which one moves.
            _draggingUpper = x > ToPosition(Value);
        }

        CaptureMouse();
        MoveThumb(x);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured)
        {
            MoveThumb(e.GetPosition(this).X);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int step = e.Key == Key.Left || e.Key == Key.Down ? -1 : e.Key == Key.Right || e.Key == Key.Up ? 1 : 0;
        if (step == 0)
        {
            return;
        }

        SetThumb((_draggingUpper ? UpperValue : Value) + step);
        e.Handled = true;
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(TrackSlider), new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    private static DependencyProperty RegisterValue(string name) =>
        DependencyProperty.Register(
            name,
            typeof(double),
            typeof(TrackSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void MoveThumb(double x) => SetThumb(ToValue(x));

    private void SetThumb(double value)
    {
        double snapped = Math.Round(Math.Max(Minimum, Math.Min(Maximum, value)));
        if (!IsRange)
        {
            Value = snapped;
        }
        else if (_draggingUpper)
        {
            UpperValue = Math.Max(snapped, Value);
        }
        else
        {
            Value = Math.Min(snapped, UpperValue);
        }
    }

    private double ToPosition(double value)
    {
        double span = ActualWidth - (2 * ThumbRadius);
        double clamped = Math.Max(Minimum, Math.Min(Maximum, value));
        return ThumbRadius + (span * ToFraction(clamped));
    }

    private double ToValue(double position)
    {
        double span = ActualWidth - (2 * ThumbRadius);
        double fraction = span <= 0 ? 0 : Math.Max(0, Math.Min(1, (position - ThumbRadius) / span));
        if (IsLogarithmic && Minimum > 0)
        {
            return Minimum * Math.Pow(Maximum / Minimum, fraction);
        }

        return Minimum + (fraction * (Maximum - Minimum));
    }

    private double ToFraction(double value)
    {
        if (Maximum <= Minimum)
        {
            return 0;
        }

        if (IsLogarithmic && Minimum > 0)
        {
            return Math.Log(value / Minimum) / Math.Log(Maximum / Minimum);
        }

        return (value - Minimum) / (Maximum - Minimum);
    }
}
