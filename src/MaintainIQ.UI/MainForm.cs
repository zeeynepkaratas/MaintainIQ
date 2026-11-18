using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace MaintainIQ.UI;

internal sealed class MainForm : XtraForm
{
    private const string LightSkin = "Office 2019 Colorful";
    private const string DarkSkin = "Office 2019 Black";

    public MainForm()
    {
        Text = "MaintainIQ";
        ClientSize = new Size(800, 450);
        StartPosition = FormStartPosition.CenterScreen;

        var grid = new GridControl { Dock = DockStyle.Fill };
        var view = new GridView(grid) { OptionsBehavior = { Editable = false } };
        grid.MainView = view;

        var themeButton = new SimpleButton
        {
            Text = "Temayı değiştir",
            Dock = DockStyle.Top,
            Height = 36
        };
        themeButton.Click += ToggleTheme;

        Controls.Add(grid);
        Controls.Add(themeButton);
        UserLookAndFeel.Default.SetSkinStyle(LightSkin);
    }

    private static void ToggleTheme(object? sender, EventArgs e)
    {
        var lookAndFeel = UserLookAndFeel.Default;
        var nextSkin = lookAndFeel.SkinName == LightSkin ? DarkSkin : LightSkin;
        lookAndFeel.SetSkinStyle(nextSkin);
    }
}
