using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Vanish.Controls;
using Vanish.Helpers;
using Vanish.Services;
using Vanish.ViewModels;

namespace Vanish.Views;

/// <summary>About Oblivion: who made it, links and what's new. Opened from the ⓘ button.</summary>
public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        VersionLine.Text = string.Format(Loc.I["About_VersionFmt"], AppInfo.Version);
        WhatsNew.ItemsSource = Enumerable.Range(1, 7).Select(i => Loc.I[$"About_New{i}"]).ToList();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!Reveal.AnimationsEnabled) return;

        // The signature "writes" itself from left to right.
        InkStop1.Offset = 0;
        InkStop2.Offset = 0.06;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var begin = TimeSpan.FromMilliseconds(180);
        InkStop1.BeginAnimation(System.Windows.Media.GradientStop.OffsetProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(1300)) { BeginTime = begin, EasingFunction = ease });
        InkStop2.BeginAnimation(System.Windows.Media.GradientStop.OffsetProperty,
            new DoubleAnimation(0.06, 1.06, TimeSpan.FromMilliseconds(1300)) { BeginTime = begin, EasingFunction = ease });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Ioc.Resolve<DialogService>().Close(false);

    private void GitHub_Click(object sender, RoutedEventArgs e) => Shell.OpenUrl(AppInfo.GitHub);

    private void Repo_Click(object sender, RoutedEventArgs e) => Shell.OpenUrl(AppInfo.Repository);

    private void Releases_Click(object sender, RoutedEventArgs e) => Shell.OpenUrl(AppInfo.Releases);
}
