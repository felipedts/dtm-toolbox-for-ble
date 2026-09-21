namespace DtmToolbox.ViewModels;

/// <summary>An entry of a dropdown: the text shown and the value it stands for.</summary>
public sealed class Option<T>
{
    public Option(string label, T value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public T Value { get; }

    public override string ToString() => Label;
}

/// <summary>What the packet type dropdown can select: a payload, or the vendor constant carrier.</summary>
public enum Payload
{
    Prbs9,
    Pattern11110000,
    Pattern10101010,
    Pattern11111111,
    ConstantCarrier,
}
