using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Windows.Forms;
namespace HudWall;
internal static class TaskbarProbe {
 internal static void Status(string path){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,JsonSerializer.Serialize(new{state=TaskbarNative.ShellState(),bars=TaskbarNative.ShellBars().Select(w=>new{handle=w.ToInt64(),visible=TaskbarNative.IsWindowVisible(w)}).ToArray(),recoveryPending=File.Exists(TaskbarRecovery.StatePath)}));}
 internal static void Run(string folder,bool hold){
  Directory.CreateDirectory(folder);string assets=Path.Combine(folder,"assets");Directory.CreateDirectory(assets);new HudTaskbarSettings{Enabled=true,Pins=new(){Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")}}.Save(assets);
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();using var icon=Icon.ExtractAssociatedIcon(Path.Combine(AppContext.BaseDirectory,"HudWall.exe"));using var form=new Form{Text="HUD taskbar validation",Icon=icon,Width=420,Height=120,StartPosition=FormStartPosition.CenterScreen};
  HudTaskbars manager=null;Process fixture=null;int before=TaskbarNative.ShellState();Exception failure=null;
  form.Shown+=async(_,_)=>{try{
   manager=new HudTaskbars(assets,icon,s=>(s.Primary?"JARVIS":"SHIELD",new(){["accent"]="#38DBFF",["text"]="#EFFBFF"}),_=>{},()=>manager?.Dispose());manager.Start();await Task.Delay(1500);
   if(manager.Bars.Count!=Screen.AllScreens.Length||TaskbarNative.ShellBars().Any(TaskbarNative.IsWindowVisible))throw new Exception("Native taskbars were not replaced on all displays: "+JsonSerializer.Serialize(new{custom=manager.Bars.Count,screens=Screen.AllScreens.Length,native=TaskbarNative.ShellBars().Select(w=>new{hwnd=w.ToInt64(),name=TaskbarNative.Class(w),visible=TaskbarNative.IsWindowVisible(w)}).ToArray(),recovery=File.Exists(TaskbarRecovery.StatePath),state=TaskbarNative.ShellState()}));
   foreach(var bar in manager.Bars){if(!bar.Visible||bar.Width<500||bar.Height<40)throw new Exception("HUD taskbar is not visible at the display edge");using var image=new Bitmap(bar.Width,bar.Height);bar.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"taskbar-"+bar.Left+".png"),ImageFormat.Png);}
   if(!hold){
    // Reproduce a taskbar stranded at an old resolution, then a stale display snapshot.
    var first=manager.Bars[0];first.Bounds=new Rectangle(first.Left,300,700,first.Height);await Task.Delay(1600);
    if(first.Bottom!=Screen.AllScreens.First(s=>s.DeviceName==first.DeviceName).Bounds.Bottom||first.Width!=Screen.AllScreens.First(s=>s.DeviceName==first.DeviceName).Bounds.Width)throw new Exception("Periodic appbar position repair failed");
    typeof(HudTaskbars).GetField("_topology",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(manager,"old:0,0,1024,768:True");await Task.Delay(1600);
    if(manager.Bars.Count!=Screen.AllScreens.Length||manager.Bars.Any(b=>b.Bottom!=Screen.AllScreens.First(s=>s.DeviceName==b.DeviceName).Bounds.Bottom))throw new Exception("Stale monitor snapshot did not rebuild taskbars");
    var small=HudTaskbars.DisplayKey(new[]{("a",new Rectangle(0,0,1024,768),true)});var dual=HudTaskbars.DisplayKey(new[]{("a",new Rectangle(0,0,2560,1440),true),("b",new Rectangle(2560,0,2560,1440),false)});if(small==dual)throw new Exception("Display signature misses resolution/count changes");
   }
   File.WriteAllText(Path.Combine(folder,"active.json"),JsonSerializer.Serialize(new{owner=Environment.ProcessId,displays=manager.Bars.Count,watchdog=File.Exists(TaskbarRecovery.StatePath),before,state=TaskbarNative.ShellState()}));
   if(hold)return;
   fixture=Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"HudWall.exe"),"--taskbar-window-fixture"){UseShellExecute=false});
   for(int i=0;i<30;i++){fixture.Refresh();if(fixture.MainWindowHandle!=IntPtr.Zero)break;await Task.Delay(100);}
   var handle=fixture.MainWindowHandle;if(handle==IntPtr.Zero||!TaskbarNative.Windows().Any(w=>w.Window==handle))throw new Exception("Live running-app enumeration missed the controlled window");
   TaskbarNative.ShowWindow(handle,6);await Task.Delay(100);TaskbarNative.Activate(handle);await Task.Delay(200);if(TaskbarNative.IsIconic(handle)||TaskbarNative.GetForegroundWindow()!=handle)throw new Exception("Taskbar activation/restore failed");
   TaskbarNative.Activate(handle);await Task.Delay(200);if(!TaskbarNative.IsIconic(handle))throw new Exception("Active task button did not minimize the app");TaskbarNative.PostMessage(handle,0x10,IntPtr.Zero,IntPtr.Zero);await Task.Delay(300);if(!fixture.HasExited)throw new Exception("Normal app close failed");fixture.Dispose();fixture=null;

   manager.WindowsTray();await Task.Delay(300);if(!TaskbarNative.ShellBars().Any(TaskbarNative.IsWindowVisible))throw new Exception("Native notification tray fallback failed");manager.Resume();await Task.Delay(300);
   manager.Dispose();manager=null;
   if(TaskbarNative.ShellState()!=before||!TaskbarNative.ShellBars().Any(TaskbarNative.IsWindowVisible))throw new Exception("Windows taskbar was not restored after normal exit");
   File.WriteAllText(Path.Combine(folder,"PASS.txt"),"Stale display snapshot rebuild, stranded appbar geometry repair, resolution/count signature, all-display replacement, appbar placement, live running-app enumeration, activate/restore/minimize/normal close, original tray access, resume and original taskbar-state restoration: PASS");
  }catch(Exception ex){failure=ex;File.WriteAllText(Path.Combine(folder,"FAILED.txt"),ex.ToString());}finally{if(!hold||failure!=null){manager?.Dispose();if(fixture!=null){if(!fixture.HasExited)fixture.Kill();fixture.Dispose();}form.Close();}}};
  Application.Run(form);if(failure!=null)throw failure;
 }
}
