// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HellboardCompanion.App.Views;

public sealed partial class ToastWindow : Window
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);

    public ToastWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows a notification in the top right corner of the primary screen for a few seconds.</summary>
    public static void Show(string title, string body)
    {
        var toast = new ToastWindow();
        toast.TitleText.Text = title;
        toast.BodyText.Text = body;
        toast.Opened += (_, _) =>
        {
            if (toast.Screens.Primary is { } screen)
            {
                var area = screen.WorkingArea;
                var size = PixelSize.FromSize(toast.ClientSize, screen.Scaling);
                toast.Position = new PixelPoint(area.Right - size.Width - 24, area.Y + 24);
            }

            DispatcherTimer.RunOnce(toast.Close, Duration);
        };
        toast.Show();
    }
}
