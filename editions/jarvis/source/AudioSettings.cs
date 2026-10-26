using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace HudWall;

internal static class AudioSettings
{
    internal static int Load(string assets)
    {
        try
        {
            using var json=JsonDocument.Parse(SettingsStorage.Read(Path.Combine(assets,"audio.json")));
            return json.RootElement.TryGetProperty("sensitivity",out var value) && value.TryGetInt32(out int percent) ? Math.Clamp(percent,0,300) : 100;
        }
        catch(Exception ex) when(ex is IOException || ex is JsonException) {return 100;}
    }
    internal static void Save(string assets,int sensitivity)
    {
        Directory.CreateDirectory(assets);
        SettingsStorage.Write(Path.Combine(assets,"audio.json"),JsonSerializer.Serialize(new { sensitivity=Math.Clamp(sensitivity,0,300) }));
    }
}

internal sealed class AudioSensitivityDialog : Form
{
    private readonly TrackBar _slider;
    internal int Sensitivity => _slider.Value;
    internal AudioSensitivityDialog(int sensitivity)
    {
        Text="HUD Live audio sensitivity"; ClientSize=new Size(450,225);
        StartPosition=FormStartPosition.CenterScreen; FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false; MinimizeBox=false;
        Controls.Add(new Label {Text="Adjust the spectrum and audio-reactive glow.\r\nThis does not change your playback volume.",Location=new Point(20,15),Size=new Size(410,40)});
        var value=new Label {Location=new Point(20,65),Size=new Size(410,25),Text=sensitivity+"%"};
        _slider=new TrackBar {Minimum=0,Maximum=300,Value=Math.Clamp(sensitivity,0,300),TickFrequency=50,SmallChange=5,LargeChange=25,Location=new Point(15,95),Size=new Size(420,50)};
        _slider.ValueChanged+=(_,_)=>value.Text=_slider.Value+"%";
        Controls.Add(value); Controls.Add(_slider);
        var save=new Button {Text="Save and apply",Location=new Point(260,170),Size=new Size(170,35),DialogResult=DialogResult.OK};
        Controls.Add(save); AcceptButton=save;
    }
}
