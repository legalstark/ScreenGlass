using System;
using System.Windows;
using System.Windows.Controls;
namespace ScreenGlass.Controls;
public partial class DisplayPanel : UserControl
{
    private MainWindow? controller;
    private bool refreshing;
    public DisplayPanel() { InitializeComponent(); }
    internal void Attach(MainWindow owner)
    {
        controller = owner;
        // Attach can run after Loaded; subscribe now so hot-plug updates reach this panel.
        owner.ControlsChanged -= Refresh;
        owner.ControlsChanged += Refresh;
        Loaded += (_, _) => { owner.ControlsChanged -= Refresh; owner.ControlsChanged += Refresh; Refresh(); };
        Unloaded += (_, _) => owner.ControlsChanged -= Refresh;
        Refresh();
    }
    private void Refresh()
    {
        if (controller == null) return;
        refreshing = true;
        var selected = ScreenChoice.SelectedValue as string;
        if (!ReferenceEquals(ScreenChoice.ItemsSource, controller.Displays))
        {
            ScreenChoice.ItemsSource = controller.Displays;
            ScreenChoice.SelectedValue = selected;
            if (ScreenChoice.SelectedIndex < 0 && controller.Displays.Count > 0) ScreenChoice.SelectedIndex = 0;
        }
        bool found = ScreenChoice.SelectedValue is string;
        Active.IsEnabled = Dimmer.IsEnabled = Warm.IsEnabled = SoftLight.IsEnabled = found;
        if (ScreenChoice.SelectedValue is string id)
        {
            var profile = controller.Profile(id);
            Active.IsChecked = profile.Enabled;
            Dimmer.Value = profile.Dimming;
            Warm.IsChecked = profile.Warm;
            SoftLight.IsChecked = profile.SoftLight;
            Amount.Text = $"{profile.Dimming:0}%";
        }
        refreshing = false;
    }
    private void SelectScreen(object sender, SelectionChangedEventArgs e) { if (!refreshing) Refresh(); }
    private void Change(object sender, RoutedEventArgs e) => Commit();
    private void DimChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Commit();
    private void Commit()
    {
        if (!refreshing && controller != null && ScreenChoice.SelectedValue is string id)
            controller.ChangeDisplay(id, Active.IsChecked == true, Dimmer.Value, Warm.IsChecked == true, SoftLight.IsChecked == true);
    }
}
