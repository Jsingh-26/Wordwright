using System.ComponentModel;
using System.Windows;

namespace Wordwright.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Closing hides to the tray; the app keeps running. P1.2 adds the tray menu's Quit.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}