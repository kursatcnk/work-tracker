using System.Windows.Controls;

namespace GorevTakip.Controls;

public partial class EsnLogo : UserControl
{
    public EsnLogo()
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
