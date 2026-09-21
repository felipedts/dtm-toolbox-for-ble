using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        LogList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopy, OnCanCopy));
    }

    /// <summary>Copies every line of the log to the clipboard.</summary>
    public void CopyAll() => CopyToClipboard(LogList.Items.OfType<LogEntry>());

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

    private void OnCanCopy(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = LogList.SelectedItems.Count > 0;

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        // SelectedItems comes in click order. The log order is the order of Items.
        var selected = new HashSet<object>(LogList.SelectedItems.Cast<object>());
        CopyToClipboard(LogList.Items.OfType<LogEntry>().Where(entry => selected.Contains(entry)));
    }

    private void OnCopyAllClick(object sender, RoutedEventArgs e) => CopyAll();

    private static void CopyToClipboard(IEnumerable<LogEntry> entries)
    {
        string text = string.Join(Environment.NewLine, entries.Select(entry => entry.ToString()));
        if (text.Length == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetDataObject(text, copy: true);
        }
        catch (ExternalException)
        {
            // Another program holds the clipboard open. Copying again works once it lets go.
        }
    }
}
