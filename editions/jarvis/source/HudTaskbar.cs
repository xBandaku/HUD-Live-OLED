using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
namespace HudWall;
internal sealed class HudTaskbarSettings {
 public bool Enabled {get;set;}=false;
 public List<string> Pins {get;set;}=new();
 internal static HudTaskbarSettings Load(string assets){try{var s=JsonSerializer.Deserialize<HudTaskbarSettings>(SettingsStorage.Read(Path.Combine(assets,"taskbar.json")))??new();s.Pins??=new();return s;}catch{return new();}}
 internal void Save(string assets)=>SettingsStorage.Write(Path.Combine(assets,"taskbar.json"),JsonSerializer.Serialize(this));
}
internal sealed class HudTaskbars:IDisposable {
 private readonly string _assets,_token=Guid.NewGuid().ToString("N");
 private readonly Icon _icon;
 private readonly Func<Screen,(string style,Dictionary<string,string> colors)> _theme;
 private readonly Action<Point> _menu;
 private readonly Action _restore;
 private readonly List<HudTaskbarWindow> _bars=new();
 private readonly Dictionary<IntPtr,Image> _icons=new();
 private readonly System.Windows.Forms.Timer _timer=new(){Interval=200};
 private DateTime _nextTasks;
 private string _topology;
 internal event Action LayoutChanged;
 internal static string DisplayKey(IEnumerable<(string id,Rectangle bounds,bool primary)> displays)=>string.Join("|",displays.OrderBy(s=>s.id,StringComparer.Ordinal).Select(s=>$"{s.id}:{s.bounds.X},{s.bounds.Y},{s.bounds.Width},{s.bounds.Height}:{s.primary}"));
 private static string CurrentTopology()=>DisplayKey(Screen.AllScreens.Select(s=>(s.DeviceName,s.Bounds,s.Primary)));
 private EventWaitHandle _stop;
 private IntPtr _shell;
 private List<IntPtr> _nativeBars=new();
 private DateTime _temporary;
 private bool _disposed;
 internal IReadOnlyList<HudTaskbarWindow> Bars=>_bars;
 internal HudTaskbarSettings Settings {get;private set;}
 internal HudTaskbars(string assets,Icon icon,Func<Screen,(string,Dictionary<string,string>)> theme,Action<Point> menu,Action restore){_assets=assets;_icon=icon;_theme=theme;_menu=menu;_restore=restore;Settings=HudTaskbarSettings.Load(assets);}
 internal void Start(){
  if(File.Exists(TaskbarRecovery.RequestPath))File.Delete(TaskbarRecovery.RequestPath);
  TaskbarRecovery.Restore();int state=TaskbarNative.ShellState();TaskbarRecovery.Save(new(Environment.ProcessId,_token,state));
  _stop=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\HudWall.TaskbarStop."+_token);
  var watchdog=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"HudWall.exe"),"--taskbar-watchdog "+Environment.ProcessId+" "+_token){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
  using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\HudWall.TaskbarReady."+_token)){Process.Start(watchdog)?.Dispose();if(!ready.WaitOne(3000))throw new InvalidOperationException("The taskbar recovery watchdog did not become ready.");}
  if(Settings.Pins.Count==0){string pinned=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Internet Explorer","Quick Launch","User Pinned","TaskBar");if(Directory.Exists(pinned))Settings.Pins=Directory.GetFiles(pinned,"*.lnk").Take(12).ToList();if(Settings.Pins.Count==0)Settings.Pins=new(){Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe")};Settings.Save(_assets);}
  try{
   TaskbarNative.ShellState(state|1);
   foreach(var screen in Screen.AllScreens){var bar=new HudTaskbarWindow(this,screen,_icon);_bars.Add(bar);bar.Show();bar.Reserve();}
   _topology=CurrentTopology();
   if(_bars.Count==0)throw new InvalidOperationException("No display taskbars were created.");
   _nativeBars=TaskbarNative.ShellBars();foreach(var window in _nativeBars)TaskbarNative.ShowWindow(window,0);
   _shell=TaskbarNative.FindWindow("Shell_TrayWnd",null);_timer.Tick+=(_,_)=>Tick();_timer.Start();Tick();
  }catch{Dispose();throw;}
 }
 private void Tick(){
  if(_disposed)return;
  try{
   if(File.Exists(TaskbarRecovery.RequestPath)){File.Delete(TaskbarRecovery.RequestPath);Dispose();_restore();return;}
   if(_temporary>DateTime.UtcNow)return;
   if(_temporary!=default){_temporary=default;TaskbarNative.ShellState(TaskbarNative.ShellState()|1);foreach(var bar in _bars){bar.Show();bar.Reserve();}}
   var shell=TaskbarNative.FindWindow("Shell_TrayWnd",null);
   if(shell!=_shell){_shell=shell;foreach(var bar in _bars)bar.RegisterAgain();}
   foreach(var window in _nativeBars)if(TaskbarNative.IsWindowVisible(window))TaskbarNative.ShowWindow(window,0);
   if(DateTime.UtcNow>=_nextTasks){_nextTasks=DateTime.UtcNow.AddSeconds(1);var topology=CurrentTopology();
    if(topology!=_topology){
     foreach(var bar in _bars){bar.Release();bar.Close();bar.Dispose();}_bars.Clear();
     foreach(var screen in Screen.AllScreens){var bar=new HudTaskbarWindow(this,screen,_icon);_bars.Add(bar);bar.Show();bar.Reserve();}
     _topology=topology;Program.Log("HUD taskbar display layout refreshed: "+topology);LayoutChanged?.Invoke();
    }
    // Explorer can add a secondary taskbar without replacing its primary window.
    _nativeBars=TaskbarNative.ShellBars();
    foreach(var window in _nativeBars)if(TaskbarNative.IsWindowVisible(window))TaskbarNative.ShowWindow(window,0);
    var tasks=TaskbarNative.Windows();foreach(var bar in _bars){bar.EnsurePosition();bar.RefreshTasks(tasks);}foreach(var key in _icons.Keys.Where(k=>!tasks.Any(t=>t.Window==k)).ToArray()){_icons[key].Dispose();_icons.Remove(key);}}
   foreach(var window in _nativeBars)if(TaskbarNative.IsWindowVisible(window))TaskbarNative.ShowWindow(window,0);
  }catch(Exception ex){Program.Log("HUD taskbar update: "+ex.GetType().Name);Dispose();_restore();}
 }
 internal (string style,Dictionary<string,string> colors) Theme(Screen screen)=>_theme(screen);
 internal void Refresh(){Settings=HudTaskbarSettings.Load(_assets);foreach(var bar in _bars)bar.Rebuild();Tick();}
 internal void HudMenu(Point point)=>_menu(point);
 internal void RestoreWindows()=>_restore();
 internal void Resume(){if(_temporary!=default){_temporary=DateTime.UtcNow.AddMilliseconds(-1);Tick();}}
 internal void WindowsTray(){
  foreach(var bar in _bars){bar.Release();bar.Hide();}
  if(File.Exists(TaskbarRecovery.StatePath)){var state=JsonSerializer.Deserialize<TaskbarRecoveryState>(File.ReadAllText(TaskbarRecovery.StatePath));if(state!=null)TaskbarNative.ShellState(state.OriginalState);}
  foreach(var window in TaskbarNative.ShellBars())TaskbarNative.ShowWindow(window,5);_temporary=DateTime.UtcNow.AddSeconds(25);
 }
 internal void Pin(string path,bool add){if(add&&!Settings.Pins.Contains(path,StringComparer.OrdinalIgnoreCase))Settings.Pins.Add(path);if(!add)Settings.Pins.RemoveAll(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase));Settings.Save(_assets);foreach(var bar in _bars)bar.Rebuild();}
 internal Image WindowIcon(IntPtr window){
  if(_icons.TryGetValue(window,out var icon))return icon;
  TaskbarNative.SendMessageTimeout(window,0x7f,new IntPtr(2),IntPtr.Zero,2,25,out var handle);if(handle==IntPtr.Zero)TaskbarNative.SendMessageTimeout(window,0x7f,new IntPtr(1),IntPtr.Zero,2,25,out handle);if(handle==IntPtr.Zero)handle=TaskbarNative.GetClassLong(window,-14);
  try{if(handle!=IntPtr.Zero){using var copy=(Icon)Icon.FromHandle(handle).Clone();return _icons[window]=copy.ToBitmap();}}catch{}
  return _icons[window]=SystemIcons.Application.ToBitmap();
 }
 internal static string Exe(uint pid){try{using var p=Process.GetProcessById((int)pid);return p.MainModule?.FileName;}catch{return null;}}
 public void Dispose(){if(_disposed)return;_disposed=true;_timer.Stop();_timer.Dispose();foreach(var bar in _bars){bar.Release();bar.Close();bar.Dispose();}_bars.Clear();TaskbarRecovery.Restore(_token);_stop?.Set();_stop?.Dispose();foreach(var image in _icons.Values)image.Dispose();_icons.Clear();}
}
internal sealed class HudTaskbarWindow:Form {
 private readonly HudTaskbars _owner;
 private readonly Screen _screen;
 private readonly FlowLayoutPanel _left=new(){Dock=DockStyle.Left,WrapContents=false},_tasks=new(){Dock=DockStyle.Fill,WrapContents=false},_right=new(){Dock=DockStyle.Right,WrapContents=false};
 private readonly ToolTip _tips=new();
 private readonly List<Image> _pinImages=new();
 private Button _clock;
 private string _signature="";
 private Color _accent,_text,_background;
 private bool _registered,_positioning,_pending,_reserved;
 private readonly uint _created=TaskbarNative.RegisterWindowMessage("TaskbarCreated");
 private const uint Callback=0x8000+441;
 internal string DeviceName=>_screen.DeviceName;
 internal HudTaskbarWindow(HudTaskbars owner,Screen screen,Icon icon){
  _owner=owner;_screen=screen;Icon=(Icon)icon.Clone();Text="HudWall taskbar / "+screen.DeviceName;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;
  Bounds=new Rectangle(screen.Bounds.Left,screen.Bounds.Bottom-62,screen.Bounds.Width,62);Controls.Add(_tasks);Controls.Add(_right);Controls.Add(_left);Rebuild();
 }
 protected override bool ShowWithoutActivation=>true;
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80|0x08000000;return p;}}
 private Color ColorOf(Dictionary<string,string> colors,string key,string fallback){try{return ColorTranslator.FromHtml(colors.TryGetValue(key,out var value)?value:fallback);}catch{return ColorTranslator.FromHtml(fallback);}}
 private Button Button(string text,int width,Action action){var button=new HudDockButton{Text=text,Width=width,Height=Height-16,Margin=new Padding(3,8,3,8),BackColor=_background,ForeColor=_text,Accent=_accent,Font=new Font("Segoe UI",10),TabStop=false};button.Click+=(_,_)=>action();return button;}
 private Button Glyph(string glyph,string tip,Action action){var b=Button(glyph,44,action);b.Font=new Font("Segoe Fluent Icons",18);b.AccessibleName=tip;((HudDockButton)b).Symbol=tip;_tips.SetToolTip(b,tip);return b;}
 internal void Rebuild(){
  var (style,colors)=_owner.Theme(_screen);_accent=ColorOf(colors,"accent","#38DBFF");_text=ColorOf(colors,"text","#EFFBFF");_background=style=="SHIELD"?Color.FromArgb(5,19,30):Color.FromArgb(2,13,23);BackColor=_background;
  foreach(Control c in _left.Controls.Cast<Control>().ToArray())c.Dispose();_left.Controls.Clear();foreach(Control c in _right.Controls.Cast<Control>().ToArray())c.Dispose();_right.Controls.Clear();foreach(var image in _pinImages)image.Dispose();_pinImages.Clear();
  bool compact=Width<1400;
  _left.Controls.Add(new Label{Text=style=="SHIELD"?"SHIELD":"JARVIS",ForeColor=_accent,Font=new Font("Segoe UI Semibold",11),Width=compact?76:112,Height=Height,TextAlign=ContentAlignment.MiddleCenter});
  _left.Controls.Add(Glyph("\uE782","Start",()=>TaskbarNative.Chord(0x5b)));if(Width>=1000)_left.Controls.Add(Glyph("\uE721","Search",()=>TaskbarNative.Chord(0x5b,0x53)));if(!compact)_left.Controls.Add(Glyph("\uE7C4","Task view",()=>TaskbarNative.Chord(0x5b,0x09)));
  foreach(string pin in _owner.Settings.Pins.Take(compact?4:10)){
   if(!File.Exists(pin))continue;var path=pin;var button=Button("",48,()=>{try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,"Could not open pinned app");}});
   try{using var icon=Icon.ExtractAssociatedIcon(path);var image=new Bitmap(icon.ToBitmap(),new Size(30,30));button.Image=image;_pinImages.Add(image);}catch{button.Text=Path.GetFileNameWithoutExtension(path)[..1];}
   button.AccessibleName=Path.GetFileNameWithoutExtension(path);_tips.SetToolTip(button,button.AccessibleName);var context=new ContextMenuStrip();context.Items.Add("Unpin",null,(_,_)=>_owner.Pin(path,false));button.ContextMenuStrip=context;_left.Controls.Add(button);
  }
  _left.Width=_left.Controls.Cast<Control>().Sum(c=>c.Width+c.Margin.Horizontal)+5;
  _right.Controls.Add(Glyph("\uE713","HUD settings",()=>_owner.HudMenu(Cursor.Position)));
  _right.Controls.Add(Glyph("\uE7F4","Quick settings",()=>TaskbarNative.Chord(0x5b,0x41)));if(!compact)_right.Controls.Add(Glyph("\uE7E7","Notifications",()=>TaskbarNative.Chord(0x5b,0x4e)));
  _right.Controls.Add(Glyph("\uE712","Windows tray (25 seconds)",()=>_owner.WindowsTray()));_right.Controls.Add(Glyph("\uE8A5","Show desktop",()=>TaskbarNative.Chord(0x5b,0x44)));
  _clock=Button("",126,()=>TaskbarNative.Chord(0x5b,0x4e));_right.Controls.Add(_clock);_right.Width=_right.Controls.Cast<Control>().Sum(c=>c.Width+c.Margin.Horizontal)+5;_signature="";Invalidate();
 }
 internal void Reserve(){if(!_registered){var data=TaskbarNative.Data(Handle);data.Callback=Callback;_registered=TaskbarNative.SHAppBarMessage(0,ref data)!=UIntPtr.Zero;if(!_registered)throw new InvalidOperationException("Windows rejected HUD taskbar registration.");}PositionBar();}
 internal void EnsurePosition(){PositionBar();}
 internal void RegisterAgain(){_registered=false;_reserved=false;Reserve();}
 internal void Release(){if(_registered){var data=TaskbarNative.Data(Handle);TaskbarNative.SHAppBarMessage(1,ref data);_registered=false;_reserved=false;}}
 private void PositionBar(){if(_positioning||!_registered)return;_positioning=true;try{int height=Math.Max(54,(int)Math.Round(62*DeviceDpi/96d));var data=TaskbarNative.Data(Handle);data.Edge=3;var b=_screen.Bounds;data.Bounds=new(){Left=b.Left,Top=b.Bottom-height,Right=b.Right,Bottom=b.Bottom};TaskbarNative.SHAppBarMessage(2,ref data);data.Bounds.Top=data.Bounds.Bottom-height;var target=Rectangle.FromLTRB(data.Bounds.Left,data.Bounds.Top,data.Bounds.Right,data.Bounds.Bottom);if(_reserved&&Bounds==target)return;TaskbarNative.SHAppBarMessage(3,ref data);Bounds=Rectangle.FromLTRB(data.Bounds.Left,data.Bounds.Top,data.Bounds.Right,data.Bounds.Bottom);_reserved=true;}finally{_positioning=false;}}
 internal void RefreshTasks(List<TaskWindow> windows){
  var foreground=TaskbarNative.GetForegroundWindow();bool full=false;if(foreground!=IntPtr.Zero&&TaskbarNative.Class(foreground) is not "Progman" and not "WorkerW"&&TaskbarNative.GetWindowRect(foreground,out var rect)){var b=_screen.Bounds;full=rect.Left<=b.Left+1&&rect.Top<=b.Top+1&&rect.Right>=b.Right-1&&rect.Bottom>=b.Bottom-1;}
  if(full){Hide();return;}if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;if(!Visible)Show();
  _clock.Text=_screen.Primary?DateTime.Now.ToString(ClockSettings.Load(Path.Combine(AppContext.BaseDirectory,"assets"))=="24"?"HH:mm\nMMM d, ddd":"h:mm tt\nMMM d, ddd"):"Calendar";
  var local=windows.Where(w=>Screen.FromHandle(w.Window).DeviceName==_screen.DeviceName).ToArray();int capacity=Math.Max(0,(_tasks.ClientSize.Width-60)/54);
  string signature=string.Join("|",local.Select(w=>w.Window+":"+w.Title))+":"+capacity;
  if(signature!=_signature){_signature=signature;foreach(Control c in _tasks.Controls.Cast<Control>().ToArray())c.Dispose();_tasks.Controls.Clear();
   foreach(var item in local.Take(capacity)){var task=item;var button=Button("",48,()=>TaskbarNative.Activate(task.Window));button.Image=_owner.WindowIcon(task.Window);button.AccessibleName=item.Title;_tips.SetToolTip(button,item.Title);button.Tag=item.Window;var context=new ContextMenuStrip();context.Items.Add("Activate / minimize",null,(_,_)=>TaskbarNative.Activate(task.Window));context.Items.Add("Close normally",null,(_,_)=>TaskbarNative.PostMessage(task.Window,0x10,IntPtr.Zero,IntPtr.Zero));string path=HudTaskbars.Exe(item.Process);if(path!=null)context.Items.Add("Pin this app",null,(_,_)=>_owner.Pin(path,true));button.ContextMenuStrip=context;_tasks.Controls.Add(button);}
   var all=Button("\uE10C",48,()=>{var menu=new ContextMenuStrip();foreach(var task in windows){var item=task;menu.Items.Add(item.Title,null,(_,_)=>TaskbarNative.Activate(item.Window));}menu.Show(Cursor.Position);});all.Font=new Font("Segoe Fluent Icons",18);_tips.SetToolTip(all,"All running windows");_tasks.Controls.Add(all);
  }
  foreach(var button in _tasks.Controls.OfType<HudDockButton>()){button.Active=button.Tag is IntPtr hwnd&&hwnd==foreground;button.Running=button.Tag is IntPtr;button.Invalidate();}
 }
 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(_owner!=null&&IsHandleCreated)Rebuild();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(Color.FromArgb(65,_accent),1);e.Graphics.DrawLine(pen,20,0,Width-20,0);}
 protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if((uint)m.Msg==Callback&&m.WParam.ToInt32()==1&&!_pending){_pending=true;BeginInvoke(()=>{_pending=false;PositionBar();});}if((uint)m.Msg==_created)RegisterAgain();base.WndProc(ref m);}
 protected override void Dispose(bool disposing){if(disposing){Release();_tips.Dispose();foreach(var image in _pinImages)image.Dispose();}base.Dispose(disposing);}
}

// Owner drawn dock tiles avoid Windows classic button chrome at every DPI.
internal sealed class HudDockButton:Button {
 internal Color Accent; internal string Symbol; internal bool Active,Running; private bool _hover;
 internal HudDockButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){_hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){_hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(BackColor);
  var r=new RectangleF(1,1,Width-2,Height-2);using var path=new System.Drawing.Drawing2D.GraphicsPath();float d=14;path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();
  if(_hover||Active){using var brush=new SolidBrush(Color.FromArgb(Active?38:23,Accent));g.FillPath(brush,path);}
  if(Symbol=="Start") {using var brush=new SolidBrush(ForeColor);float cx=Width/2f,cy=Height/2f;for(int x=0;x<2;x++)for(int y=0;y<2;y++)g.FillRectangle(brush,cx-10+x*11,cy-10+y*11,9,9);}
  else if(Symbol=="Task view"){using var pen=new Pen(ForeColor,1.6f);g.DrawRectangle(pen,Width/2-12,Height/2-8,17,16);g.DrawRectangle(pen,Width/2+8,Height/2-8,4,16);}
  else if(Image!=null){int size=Math.Min(30,Height-14);g.DrawImage(Image,(Width-size)/2,(Height-size)/2-2,size,size);}else TextRenderer.DrawText(g,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
  if(Running){using var pen=new Pen(Active?Accent:Color.FromArgb(110,ForeColor),3){StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round};g.DrawLine(pen,Width/2-(Active?8:2),Height-3,Width/2+(Active?8:2),Height-3);}
 }
}
