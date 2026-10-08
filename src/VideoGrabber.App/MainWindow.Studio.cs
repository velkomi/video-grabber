using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private static readonly FontFamily StudioBodyFont = new("ms-appx:///Assets/Fonts/Onest.ttf#Onest");
    private static readonly FontFamily StudioHeadingFont = new("ms-appx:///Assets/Fonts/Manrope.ttf#Manrope");
    private static readonly SolidColorBrush StudioLinkBrush = new(ColorHelper.FromArgb(255, 97, 168, 255));
    private readonly Dictionary<string, Button> _navigationItems = new();

    private static void InitializeStudioPresentation()
    {
        var resources = Application.Current.Resources;
        resources["ContentControlThemeFontFamily"] = StudioBodyFont;
        resources["ButtonFontFamily"] = StudioBodyFont;
        resources["TextFillColorPrimaryBrush"] = TextBrush;
        resources["TextFillColorSecondaryBrush"] = MutedBrush;
        foreach (var key in new[] { "ButtonBackground", "TextControlBackground", "ComboBoxBackground" })
            resources[key] = CardBrush;
        foreach (var key in new[] { "ButtonBorderBrush", "TextControlBorderBrush", "ComboBoxBorderBrush" })
            resources[key] = CardBorderBrush;
        foreach (var key in new[] { "ButtonForeground", "TextControlForeground", "ComboBoxForeground" })
            resources[key] = TextBrush;
        resources["ButtonBackgroundPointerOver"] = BadgeBrush;
        resources["ButtonBackgroundPressed"] = CardBrush;
        resources["ButtonForegroundPointerOver"] = TextBrush;
        resources["ButtonForegroundPressed"] = TextBrush;
        resources["TextControlBackgroundFocused"] = RootBackgroundBrush;
        resources["TextControlForegroundFocused"] = TextBrush;
        resources["TextControlThemeMinHeight"] = 44d;
    }

    private static void ApplyStudioInputs(UIElement root)
    {
        if (root is TextBox textBox)
        {
            textBox.FontFamily = StudioBodyFont; textBox.FontSize = 14;
            textBox.MinHeight = 44; textBox.CornerRadius = new CornerRadius(12);
            textBox.Resources["TextControlThemeMinHeight"] = 44d;
        }
        if (root is ComboBox comboBox)
        {
            comboBox.FontFamily = StudioBodyFont; comboBox.FontSize = 14;
            comboBox.MinHeight = 44; comboBox.CornerRadius = new CornerRadius(12);
            comboBox.Padding = new Thickness(12, 12, 12, 12);
            comboBox.Resources["ComboBoxMinHeight"] = 44d;
        }
        if (root is Button button)
        {
            button.Resources["ButtonBackgroundDisabled"] = CardBrush;
            button.Resources["ButtonForegroundDisabled"] = MutedBrush;
            button.Resources["ButtonBorderBrushDisabled"] = CardBorderBrush;
        }
        // Walk our logical content before template realization, leaving internal controls alone.
        if (root is Panel panel) foreach (var child in panel.Children) ApplyStudioInputs(child);
        else if (root is Border { Child: { } child }) ApplyStudioInputs(child);
        else if (root is ScrollViewer { Content: UIElement page }) ApplyStudioInputs(page);
        else if (root is ContentControl { Content: UIElement content }) ApplyStudioInputs(content);
    }

    private static LinearGradientBrush StudioPrimaryBrush() => new()
    {
        StartPoint = new Windows.Foundation.Point(0, 0),
        EndPoint = new Windows.Foundation.Point(1, 1),
        GradientStops =
        {
            new GradientStop { Color = ColorHelper.FromArgb(255, 22, 117, 207), Offset = 0 },
            new GradientStop { Color = ColorHelper.FromArgb(255, 27, 87, 200), Offset = 1 }
        }
    };

    private static Button StudioButton(string text, bool primary)
    {
        var button = new Button
        {
            Content = text, FontFamily = StudioBodyFont, FontSize = 14, FontWeight = FontWeights.SemiBold,
            MinHeight = 44, Padding = new Thickness(18, 9, 18, 9), CornerRadius = new CornerRadius(13),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = primary ? StudioPrimaryBrush() : CardBrush,
            Foreground = primary ? new SolidColorBrush(Colors.White) : TextBrush,
            BorderBrush = primary ? StudioLinkBrush : CardBorderBrush, BorderThickness = new Thickness(1)
        };
        if (primary)
        {
            button.Resources["ButtonBackgroundPointerOver"] = StudioPrimaryBrush();
            button.Resources["ButtonBackgroundPressed"] = StudioPrimaryBrush();
            button.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(Colors.White);
            button.Resources["ButtonForegroundPressed"] = new SolidColorBrush(Colors.White);
        }
        return button;
    }

    private static Button StudioGoogleButton(string text)
    {
        var button = StudioButton(text, false);
        var white = new SolidColorBrush(Colors.White);
        var ink = new SolidColorBrush(ColorHelper.FromArgb(255, 31, 31, 31));
        button.Background = white;
        button.Foreground = ink;
        button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 116, 119, 117));
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(ColorHelper.FromArgb(255, 242, 242, 242));
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(ColorHelper.FromArgb(255, 230, 230, 230));
        button.Resources["ButtonForegroundPointerOver"] = ink;
        button.Resources["ButtonForegroundPressed"] = ink;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Icons/google-g.png")),
            Width = 20, Height = 20, Stretch = Stretch.Uniform
        });
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        button.Content = content;
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static HyperlinkButton StudioLink(string label, string url) => new()
    {
        Content = label, NavigateUri = new Uri(url), FontFamily = StudioBodyFont,
        FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = StudioLinkBrush,
        Padding = new Thickness(0, 8, 0, 8), MinHeight = 40,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private static Border Details(string title, UIElement content)
    {
        var panel = Vertical(0);
        var header = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
        {
            Content = "›  " + title, FontFamily = StudioBodyFont, FontSize = 14,
            FontWeight = FontWeights.SemiBold, MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(18, 12, 18, 12), Background = CardBrush,
            Foreground = TextBrush, BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(20)
        };
        var detail = new Border { Child = content, Padding = new Thickness(18, 8, 18, 18), Visibility = Visibility.Collapsed };
        AutomationProperties.SetName(header, title);
        void UpdateDisclosure()
        {
            var expanded = header.IsChecked == true;
            detail.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            header.Content = (expanded ? "⌄  " : "›  ") + title;
            AutomationProperties.SetHelpText(header, expanded ? "Свернуть подробности" : "Открыть подробности");
        }
        header.Checked += (_, _) => UpdateDisclosure();
        header.Unchecked += (_, _) => UpdateDisclosure();
        UpdateDisclosure();
        panel.Children.Add(header);
        panel.Children.Add(detail);
        return new Border { Child = panel, Background = CardBrush, BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20) };
    }

    private void RegisterNavigation(string page, Button button)
    {
        _navigationItems[page] = button;
        ToolTipService.SetToolTip(button, ((StackPanel)button.Content).Children.OfType<TextBlock>().Single().Text);
        AutomationProperties.SetName(button, ((StackPanel)button.Content).Children.OfType<TextBlock>().Single().Text);
    }

    private void UpdateStudioNavigation(string? page)
    {
        foreach (var (key, button) in _navigationItems)
        {
            var selected = key == (page ?? "download");
            button.Background = selected ? BadgeBrush : new SolidColorBrush(Colors.Transparent);
            button.BorderBrush = selected ? StudioLinkBrush : new SolidColorBrush(Colors.Transparent);
            button.BorderThickness = new Thickness(selected ? 3 : 1, 1, 1, 1);
            AutomationProperties.SetItemStatus(button, selected ? "Текущий раздел" : "");
        }
    }
}
