using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HudWall;

internal static class IdleInfo
{
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Time; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput info);
    internal static bool IsIdle(uint milliseconds)
    {
        var info=new LastInput {Size=(uint)Marshal.SizeOf<LastInput>()};
        return GetLastInputInfo(ref info) && unchecked((uint)Environment.TickCount-info.Time)>=milliseconds;
    }
}

internal sealed partial class HudForm
{
    private void AddOledMenu(ToolStripMenuItem display,string id)
    {
        if(EditionSettings.IsJarvis)return;
        var preference=Preference(id);
        var oled=new ToolStripMenuItem("OLED care");
        var enabled=new ToolStripMenuItem("OLED mode (black background / reduced glow)") {Checked=preference.Oled};
        enabled.Click+=(_,_)=>{Preference(id).Oled=!Preference(id).Oled; if(SaveMonitors())ApplySettings(); enabled.Checked=Preference(id).Oled;};
        oled.DropDownItems.Add(enabled);
        var floating=new ToolStripMenuItem("Full-screen OLED motion (gentle drift)") {Checked=preference.OledFloat};
        floating.Click+=(_,_)=>{Preference(id).OledFloat=!Preference(id).OledFloat; if(SaveMonitors())ApplySettings();floating.Checked=Preference(id).OledFloat;};
        oled.DropDownItems.Add(floating);
        var brightness=new ToolStripMenuItem("HUD brightness") {DropDown=new ThemeDropDownMenu()};
        foreach(int percent in new[]{25,40,60,80,100})
        {
            var item=new StickyThemeMenuItem(percent+"%") {Checked=preference.OledBrightness==percent};
            item.Click+=(_,_)=>{
                Preference(id).OledBrightness=percent;
                if(SaveMonitors())ApplySettings();
                foreach(var option in brightness.DropDownItems.OfType<ToolStripMenuItem>())option.Checked=option.Text==Preference(id).OledBrightness+"%";
            };
            brightness.DropDownItems.Add(item);
        }
        brightness.DropDownItems.Add(new ToolStripSeparator());
        brightness.DropDownItems.Add(new ToolStripMenuItem("Done"));
        oled.DropDownItems.Add(brightness);
        var shift=new ToolStripMenuItem("Extra pixel shift (every minute)") {Checked=preference.PixelShift};
        shift.Click+=(_,_)=>{Preference(id).PixelShift=!Preference(id).PixelShift; if(SaveMonitors())ApplySettings(); shift.Checked=Preference(id).PixelShift;};
        oled.DropDownItems.Add(shift);
        var idle=new ToolStripMenuItem("Dim HUD after 2 minutes idle") {Checked=preference.IdleDim};
        idle.Click+=(_,_)=>{Preference(id).IdleDim=!Preference(id).IdleDim; if(SaveMonitors())ApplySettings(); idle.Checked=Preference(id).IdleDim;};
        oled.DropDownItems.Add(idle);
        oled.DropDownItems.Add(new ToolStripSeparator());
        oled.DropDownItems.Add(new ToolStripMenuItem("Applies to this HUD only; no panel/HDR changes") {Enabled=false});
        display.DropDownItems.Add(oled);
    }
}
