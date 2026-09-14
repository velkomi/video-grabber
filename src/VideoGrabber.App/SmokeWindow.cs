using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace VideoGrabber.App;

internal sealed class SmokeWindow : Window
{
    public SmokeWindow()
    {
        Title = "VideoGrabber smoke";
        Content = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 15, 17, 23)),
            Children =
            {
                new TextBlock
                {
                    Text = "VideoGrabber",
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        AppWindow.Resize(new SizeInt32(700, 450));
    }
}
