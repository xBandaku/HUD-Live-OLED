using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace HudWall;
internal static class TaskbarNative {
 [StructLayout(LayoutKind.Sequential)]internal struct Rect{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]internal struct AppBar{public uint Size;public IntPtr Window;public uint Callback,Edge;public Rect Bounds;public IntPtr Param;}
 internal delegate bool EnumProc(IntPtr window,IntPtr data);
 [DllImport("shell32.dll")]internal static extern UIntPtr SHAppBarMessage(uint message,ref AppBar data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern IntPtr FindWindow(string className,string title);
 [DllImport("user32.dll")]internal static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")]internal static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool IsZoomed(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool AttachThreadInput(uint a,uint b,bool attach);
 [DllImport("kernel32.dll")]internal static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]internal static extern IntPtr GetLastActivePopup(IntPtr window);
 [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32.dll")]internal static extern IntPtr GetWindow(IntPtr window,uint command);
 [DllImport("user32.dll")]private static extern IntPtr GetTopWindow(IntPtr parent);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern IntPtr GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="GetClassLongPtrW")]internal static extern IntPtr GetClassLong(IntPtr window,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetClassName(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern uint RegisterWindowMessage(string name);
 [DllImport("user32.dll")]internal static extern bool PostMessage(IntPtr window,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]internal static extern IntPtr SendMessageTimeout(IntPtr window,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
 [DllImport("user32.dll")]internal static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")]internal static extern bool RegisterHotKey(IntPtr window,int id,uint mods,uint key);
 [DllImport("user32.dll")]internal static extern bool UnregisterHotKey(IntPtr window,int id);
 [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr window,int attr,out int value,int size);
 internal static AppBar Data(IntPtr window)=>new(){Size=(uint)Marshal.SizeOf<AppBar>(),Window=window};
 internal static string Class(IntPtr window){var text=new StringBuilder(256);GetClassName(window,text,text.Capacity);return text.ToString();}
 internal static List<IntPtr> ShellBars(){var list=new List<IntPtr>();EnumWindows((w,_)=>{if(Class(w) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")list.Add(w);return true;},IntPtr.Zero);return list;}
 internal static int ShellState(){var data=Data(FindWindow("Shell_TrayWnd",null));return (int)SHAppBarMessage(4,ref data).ToUInt32();}
 internal static void ShellState(int state){var data=Data(FindWindow("Shell_TrayWnd",null));data.Param=(IntPtr)state;SHAppBarMessage(10,ref data);}
 internal static void Chord(params byte[] keys){foreach(byte key in keys)keybd_event(key,0,0,UIntPtr.Zero);foreach(byte key in keys.Reverse())keybd_event(key,0,2,UIntPtr.Zero);}
 internal static List<TaskWindow> Windows(){
  var list=new List<TaskWindow>();EnumWindows((w,_)=>{
   if(!IsWindowVisible(w)||GetWindow(w,4)!=IntPtr.Zero||(GetWindowLong(w,-20).ToInt64()&0x80)!=0)return true;
   GetWindowThreadProcessId(w,out uint pid);
   if(DwmGetWindowAttribute(w,14,out int cloaked,4)==0&&cloaked!=0)return true;
   var text=new StringBuilder(512);GetWindowText(w,text,text.Capacity);if(text.Length==0||(pid==Environment.ProcessId&&text.ToString()=="HUD Live")||Class(w) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")return true;
   list.Add(new(w,pid,text.ToString()));return true;
  },IntPtr.Zero);return list;
 }
 internal static List<TaskbarWindowState> VisibilityWindows(){
  var result=new List<TaskbarWindowState>();var seen=new HashSet<IntPtr>();
  var foreground=GetForegroundWindow();var window=GetTopWindow(IntPtr.Zero);
  // Bound traversal because windows can be destroyed or reordered while enumerating.
  for(int count=0;window!=IntPtr.Zero&&count<4096&&seen.Add(window);count++){
   var current=window;window=GetWindow(current,2); // GW_HWNDNEXT: descending z-order
   if(!IsWindowVisible(current)||IsIconic(current))continue;
   if(current!=foreground&&(GetWindow(current,4)!=IntPtr.Zero||(GetWindowLong(current,-20).ToInt64()&0x80)!=0))continue;
   if(DwmGetWindowAttribute(current,14,out int cloaked,4)==0&&cloaked!=0)continue;
   if(!GetWindowRect(current,out var rect)||rect.Right<=rect.Left||rect.Bottom<=rect.Top)continue;
   GetWindowThreadProcessId(current,out uint pid);
   bool caption=(GetWindowLong(current,-16).ToInt64()&0x00C00000)==0x00C00000;
   bool maximized=IsZoomed(current)&&caption;
   var bounds=System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
   if(maximized)bounds=TaskbarVisibility.VisibleBounds(bounds,System.Windows.Forms.Screen.FromHandle(current).Bounds,true);
   result.Add(new(bounds,pid==Environment.ProcessId,Class(current),maximized));
  }
  return result;
 }
 internal static void Activate(IntPtr window){if(!IsWindow(window))return;if(GetForegroundWindow()==window&&!IsIconic(window)){ShowWindow(window,6);return;}if(IsIconic(window))ShowWindow(window,9);var popup=GetLastActivePopup(window);var target=popup!=IntPtr.Zero&&IsWindowVisible(popup)?popup:window;if(!SetForegroundWindow(target)){uint current=GetCurrentThreadId(),foreground=GetWindowThreadProcessId(GetForegroundWindow(),out _);if(foreground!=0&&foreground!=current&&AttachThreadInput(current,foreground,true))try{SetForegroundWindow(target);}finally{AttachThreadInput(current,foreground,false);}}}
}
internal sealed record TaskWindow(IntPtr Window,uint Process,string Title);
internal sealed record TaskbarRecoveryState(int Owner,string Token,int OriginalState);
internal static class TaskbarRecovery {
 internal static string StatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".HudWall","taskbar-recovery.json");
 internal static string RequestPath=>Path.Combine(Path.GetDirectoryName(StatePath),"taskbar-restore.request");
 internal static void RequestRestore(){var s=HudTaskbarSettings.Load(Path.Combine(AppContext.BaseDirectory,"assets"));s.Enabled=false;s.Save(Path.Combine(AppContext.BaseDirectory,"assets"));Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));File.WriteAllText(RequestPath,"restore");Restore();}
 internal static void Save(TaskbarRecoveryState state)=>SettingsStorage.Write(StatePath,JsonSerializer.Serialize(state));
 internal static void Restore(string token=null){try{if(!File.Exists(StatePath))return;var state=JsonSerializer.Deserialize<TaskbarRecoveryState>(File.ReadAllText(StatePath));if(state==null||(token!=null&&state.Token!=token))return;TaskbarNative.ShellState(state.OriginalState);foreach(var window in TaskbarNative.ShellBars())TaskbarNative.ShowWindow(window,5);File.Delete(StatePath);}catch(Exception ex){Program.Log("taskbar recovery: "+ex.GetType().Name);}}
 internal static void Watch(int owner,string token){
  if(!Guid.TryParseExact(token,"N",out _))return;
  try{using var signal=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\HudWall.TaskbarStop."+token);using var parent=Process.GetProcessById(owner);using var ready=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\HudWall.TaskbarReady."+token);ready.Set();while(!signal.WaitOne(500)){if(parent.HasExited){Restore(token);return;}}}catch(ArgumentException){Restore(token);}catch(Exception ex){Program.Log("taskbar watchdog: "+ex.GetType().Name);Restore(token);}
 }
}
