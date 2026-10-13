using System.Windows.Controls;

namespace WorkTracker.Controls;

public partial class AppLogo : UserControl
{
    public AppLogo()
    {
        InitializeComponent();
    }

    // logonun altındaki küçük yazı (ana ekranda bugünün tarihi)
    public string Subtitle
    {
        get => SubtitleText.Text;
        set => SubtitleText.Text = value;
    }
}
