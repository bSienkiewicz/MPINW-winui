using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SupportTool.NumberRange;
using SupportTool.Features.Alerts;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool;
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Type> _pageMapping = new Dictionary<string, Type>
        {
            { "Alerting", typeof(Alerting) },
            { "NRAlertsList", typeof(AlertAudit) },
            { "NRThresholdManager", typeof(ThresholdManager) },
            { "FLRange_RoyalMail", typeof(RoyalMail) }
        };

    public MainWindow()
    {
        this.InitializeComponent();
        TrySetMicaBackdrop(false);
        ExtendsContentIntoTitleBar = true;
        ContentFrame.Navigated += OnNavigated;
        SetTitleBar(this.AppTitleBar);
        this.ContentFrame.Navigate(typeof(Alerting));
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        var targetTag = _pageMapping.FirstOrDefault(x => x.Value == e.SourcePageType).Key;
        if (targetTag != null)
        {
            var selectedItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == targetTag);

            if (selectedItem != null)
            {
                NavView.SelectedItem = selectedItem;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            // Navigate to the settings page
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }
        if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag != null)
        {
            // Navigate to the corresponding page when a NavigationViewItem is selected
            if (_pageMapping.TryGetValue(selectedItem.Tag.ToString(), out var pageType))
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }

    bool TrySetMicaBackdrop(bool useMicaAlt)
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new()
            {
                Kind = useMicaAlt ? Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt : Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            };
            this.SystemBackdrop = micaBackdrop;

            return true;
        }

        return false;
    }

    private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }
}
