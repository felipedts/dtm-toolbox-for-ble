using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DtmToolbox.ViewModels;

namespace DtmToolbox.Views;

public partial class LogPane : UserControl
{
    private MainViewModel? _viewModel;
    private bool _scrollPending;

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

    // This runs while the collection is still raising its change event, before the list box has
    // taken the new item in. Scrolling right here forces a layout pass on a list that does not
    // match its source, which WPF rejects with an exception. The scroll is posted to run after
    // the change is processed, once for any number of lines added in the meantime.
    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _scrollPending || _viewModel == null || !_viewModel.AutoScrollLog)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ScrollToLastLine));
    }

    private void ScrollToLastLine()
    {
        _scrollPending = false;
        int count = LogList.Items.Count;
        if (count > 0 && _viewModel != null && _viewModel.AutoScrollLog)
        {
            LogList.ScrollIntoView(LogList.Items[count - 1]);
        }
    }
}
