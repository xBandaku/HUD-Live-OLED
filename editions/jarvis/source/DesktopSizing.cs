using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
namespace HudWall;
internal static class DesktopSizing {
 [StructLayout(LayoutKind.Sequential)]internal struct Rect {internal int Left,Top,Right,Bottom;internal Rectangle Value=>Rectangle.FromLTRB(Left,Top,Right,Bottom);}
 [StructLayout(LayoutKind.Sequential)]private struct PointNative {internal int X,Y;}
 [DllImport("user32.dll")]private static extern int GetSystemMetrics(int index);
 [DllImport("user32.dll")]private static extern IntPtr GetParent(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr window,StringBuilder name,int count);
 [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]private static extern bool GetClientRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]private static extern int MapWindowPoints(IntPtr from,IntPtr to,ref PointNative point,uint count);
 [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]private static extern IntPtr SetParent(IntPtr window,IntPtr parent);
 internal static Rectangle VirtualBounds()=>new(GetSystemMetrics(76),GetSystemMetrics(77),GetSystemMetrics(78),GetSystemMetrics(79));
 internal static bool Ensure(IntPtr window){var parent=GetParent(window);if(parent==IntPtr.Zero)return false;var name=new StringBuilder(256);GetClassName(parent,name,name.Capacity);if(name.ToString() is not "WorkerW" and not "Progman")return false;var desired=VirtualBounds();if(desired.Width<=0||desired.Height<=0)return false;return Repair(window,parent,desired);}
 internal static bool Repair(IntPtr window,IntPtr parent,Rectangle desired){
  if(!GetWindowRect(window,out var old)||!GetClientRect(window,out var client))return false;
  if(old.Value==desired&&client.Right-client.Left==desired.Width&&client.Bottom-client.Top==desired.Height)return false;
  var origin=new PointNative{X=desired.X,Y=desired.Y};MapWindowPoints(IntPtr.Zero,parent,ref origin,1);
  if(!SetWindowPos(window,IntPtr.Zero,origin.X,origin.Y,desired.Width,desired.Height,0x0074))return false;
  Program.Log("Wallpaper bounds repaired: "+old.Value+" -> "+desired);return true;
 }
 internal static void Test(string folder){
  Directory.CreateDirectory(folder);Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);using var parent=new Form{Bounds=new Rectangle(-120,40,900,700),FormBorderStyle=FormBorderStyle.None};using var child=new Form{Bounds=new Rectangle(0,0,1600,900),FormBorderStyle=FormBorderStyle.None,AutoScaleMode=AutoScaleMode.None};var hwnd=child.Handle;SetParent(hwnd,parent.Handle);
  foreach(var target in new[]{new Rectangle(0,0,5120,1440),new Rectangle(-2560,-200,5120,1440),new Rectangle(0,0,2560,1440)}){
   child.Size=new Size(1600,900);if(!Repair(hwnd,parent.Handle,target)||!GetWindowRect(hwnd,out var rect)||rect.Value!=target)throw new Exception("Wallpaper bounds repair failed: "+target);
   if(Repair(hwnd,parent.Handle,target))throw new Exception("Stable bounds caused repeated resizing");
  }
  File.WriteAllText(Path.Combine(folder,"PASS.txt"),"Native 1600x900 preview shrink repair, 5120x1440 coverage, negative virtual-desktop origin mapping, resolution change and stable no-op checks: PASS");
 }
}
