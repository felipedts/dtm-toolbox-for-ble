using System.Reflection;
using System.Windows;

namespace DtmToolbox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + ProductVersion();
    }

    private static string ProductVersion()
    {
        var attribute = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion ?? "unknown";
    }
}
