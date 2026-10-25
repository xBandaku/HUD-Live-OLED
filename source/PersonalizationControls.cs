using System.Windows.Forms;

namespace HudWall;

internal sealed partial class HudForm
{
    private void AddPersonalizationMenu(ContextMenuStrip menu)
    {
        string assets=SettingsStore.Root;
        var themes=new ToolStripMenuItem("Theme presets") {DropDown=new ThemeDropDownMenu()};
        foreach(string name in ThemeSettings.Presets.Keys.Concat(new[]{"Custom"}))
        {
            var item=new StickyThemeMenuItem(name);
            item.Click+=(_,_)=>{
                try {ThemeSettings.ApplyPreset(assets,name); RefreshThemes(); ApplySettings();}
                catch(Exception ex){MessageBox.Show(ex.Message,"Could not apply theme");}
            };
            themes.DropDownItems.Add(item);
        }
        themes.DropDownItems.Add(new ToolStripSeparator());
        themes.DropDownItems.Add(new ToolStripMenuItem("Done"));
        void RefreshThemes()
        {
            string current=ThemeSettings.Match(ThemeSettings.Load(assets)) ?? "Custom";
            foreach(var item in themes.DropDownItems.OfType<StickyThemeMenuItem>())
            {
                item.Checked=item.Text==current;
                item.Enabled=item.Text!="Custom" || current=="Custom" || File.Exists(Path.Combine(assets,"custom-colors.json"));
            }
        }
        RefreshThemes(); menu.Opening+=(_,_)=>RefreshThemes(); menu.Items.Add(themes);
        var audio=new ToolStripMenuItem("Audio sensitivity");
        foreach(int percent in new[]{0,50,100,150,200,300})
        {
            var item=new ToolStripMenuItem(percent==0 ? "Off (0%)" : percent==100 ? "Normal (100%)" : percent+"%") {Tag=percent};
            item.Click+=(_,_)=>{
                try {AudioSettings.Save(assets,percent); RefreshAudio(); ApplySettings();}
                catch(Exception ex){MessageBox.Show(ex.Message,"Could not save audio sensitivity");}
            };
            audio.DropDownItems.Add(item);
        }
        var custom=new ToolStripMenuItem("Custom...");
        custom.Click+=(_,_)=>{
            using var dialog=new AudioSensitivityDialog(AudioSettings.Load(assets));
            if(dialog.ShowDialog()!=DialogResult.OK)return;
            try {AudioSettings.Save(assets,dialog.Sensitivity); RefreshAudio(); ApplySettings();}
            catch(Exception ex){MessageBox.Show(ex.Message,"Could not save audio sensitivity");}
        };
        audio.DropDownItems.Add(custom);
        void RefreshAudio()
        {
            int percent=AudioSettings.Load(assets);
            foreach(ToolStripMenuItem item in audio.DropDownItems) item.Checked=item.Tag is int value ? value==percent : !new[]{0,50,100,150,200,300}.Contains(percent);
            audio.Text="Audio sensitivity ("+percent+"%)";
        }
        RefreshAudio(); menu.Items.Add(audio);
    }
}
