// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HellboardCompanion.App.Views;

public sealed partial class StratagemPickerView : UserControl
{
    public StratagemPickerView()
    {
        InitializeComponent();
    }

    /// <summary>The search box has the focus as soon as the flyout opens.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Input);
    }
}
