using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DtmToolbox.Views;

public partial class AboutPane : UserControl
{
    public AboutPane()
    {
        InitializeComponent();
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
        {
            // No browser is registered for the link. The address stays readable on screen.
        }

        e.Handled = true;
    }
}
