using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DeskNest.Services;
internal static class HudWindowIcon {
 private static ImageSource _image;
 internal static ImageSource Image {
  get {
   if(_image!=null)return _image;
   using var stream=typeof(HudWall.HudForm).Assembly.GetManifestResourceStream("HudWall.hud.ico");
   var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
   var image=decoder.Frames.OrderByDescending(f=>f.PixelWidth).First();image.Freeze();return _image=image;
  }
 }
}
