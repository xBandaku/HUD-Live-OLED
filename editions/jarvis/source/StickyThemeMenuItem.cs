using System.Drawing;
using System.Windows.Forms;
namespace HudWall;
internal sealed class StickyThemeMenuItem : ToolStripMenuItem
{
    internal StickyThemeMenuItem(string text) : base(text) { }
    protected override bool DismissWhenClicked => false;
}

internal sealed class ThemeDropDownMenu : ToolStripDropDownMenu
{
    protected override void OnItemClicked(ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem is not StickyThemeMenuItem) { base.OnItemClicked(e); return; }
        bool autoClose = AutoClose;
        AutoClose = false;
        try { base.OnItemClicked(e); }
        finally { AutoClose = autoClose; }
    }
}
