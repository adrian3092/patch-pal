using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PatchPal.App.Behaviors;

/// <summary>
/// WPF-UI's NavigationView hosts each page in a NavigationViewContentPresenter (a
/// <see cref="Frame"/>) that measures content with infinite height. Star-sized rows
/// therefore grow to their full content height and inner ScrollViewers never engage —
/// long lists are clipped at the window edge with no scrollbar.
///
/// Setting <c>FillHost.Enabled="True"</c> on a page's root element binds its Height to
/// the hosting frame's ActualHeight (which IS bounded to the viewport), so the page
/// fills the visible area and its list scrolls. The frame is found via a raw visual-tree
/// walk because RelativeSource FindAncestor will not cross the Frame navigation boundary.
/// </summary>
public static class FillHost
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(FillHost),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || !(bool)e.NewValue)
            return;

        element.Loaded += (_, _) =>
        {
            if (FindHostFrame(element) is { } host)
                element.SetBinding(
                    FrameworkElement.HeightProperty,
                    new Binding(nameof(FrameworkElement.ActualHeight)) { Source = host });
        };
    }

    private static Frame? FindHostFrame(DependencyObject start)
    {
        for (var p = VisualTreeHelper.GetParent(start); p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is Frame frame)
                return frame;
        return null;
    }
}
