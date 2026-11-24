using System.Drawing;
using System.Windows.Forms;

namespace HudWall;

internal sealed partial class HudForm
{
    private bool _nativeTransparent;
    private void AddTransparencyMenu(ContextMenuStrip menu)
    {
        var item=new ToolStripMenuItem("Transparent background (Windows wallpaper)");
        void Refresh()
        {
            var selected=Screen.AllScreens.Select(screen=>Preference(screen.DeviceName).Transparent).ToArray();
            item.CheckState=selected.All(value=>value)?CheckState.Checked:selected.Any(value=>value)?CheckState.Indeterminate:CheckState.Unchecked;
        }
        item.Click+=(_,_)=>{
            bool enabled=!Screen.AllScreens.All(screen=>Preference(screen.DeviceName).Transparent);
            foreach(var screen in Screen.AllScreens)Preference(screen.DeviceName).Transparent=enabled;
            if(SaveMonitors())ApplySettings();Refresh();
        };
        Refresh();menu.Opening+=(_,_)=>Refresh();menu.Items.Add(item);
    }
    private void ApplyNativeTransparency()
    {
        bool enabled=_monitorStates.Any(m=>m.transparent);
        if(enabled==_nativeTransparent)return;
        _nativeTransparent=enabled;
        if(enabled)
        {
            BackColor=Color.Magenta;_web.BackColor=Color.Magenta;
            TransparencyKey=Color.Magenta;_web.DefaultBackgroundColor=Color.Transparent;
        }
        else
        {
            TransparencyKey=Color.Empty;BackColor=Color.Black;
            _web.BackColor=Color.Black;_web.DefaultBackgroundColor=Color.Black;
        }
    }
}
