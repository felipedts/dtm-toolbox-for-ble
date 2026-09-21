using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DtmToolbox.Controls;

/// <summary>
/// Attached behavior for a text box bound to a number: Enter commits the text, and text that
/// is not a number goes back to the last valid value when the box loses focus.
/// </summary>
public static class NumericInput
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(NumericInput), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (!(element is TextBox textBox))
        {
            return;
        }

        textBox.KeyDown -= OnKeyDown;
        textBox.LostFocus -= OnLostFocus;
        textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
        if ((bool)e.NewValue)
        {
            textBox.KeyDown += OnKeyDown;
            textBox.LostFocus += OnLostFocus;
            textBox.GotKeyboardFocus += OnGotKeyboardFocus;
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            Commit(textBox);
            textBox.SelectAll();
            e.Handled = true;
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Commit(textBox);
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private static void Commit(TextBox textBox)
    {
        BindingExpression? binding = textBox.GetBindingExpression(TextBox.TextProperty);
        if (binding == null)
        {
            return;
        }

        // Reading back shows the value the source kept: clamped to its range, or the last valid
        // one when the text was not a number.
        binding.UpdateSource();
        binding.UpdateTarget();
    }
}
