using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using DtmToolbox.ViewModels;

namespace DtmToolbox.Views;

public partial class LogPane : UserControl
{
    private MainViewModel? _viewModel;

    public LogPane()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Log.CollectionChanged -= OnLogChanged;
        }

        _viewModel = e.NewValue as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.Log.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _viewModel != null && _viewModel.AutoScrollLog && _viewModel.Log.Count > 0)
        {
            LogList.ScrollIntoView(_viewModel.Log[_viewModel.Log.Count - 1]);
        }
    }
}
