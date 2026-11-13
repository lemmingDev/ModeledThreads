using System.Drawing;
using System.Windows.Forms;

namespace ModeledThreads.UI;

/// <summary>
/// Turns a drawing into the picture object Inventor wants for a ribbon icon.
/// </summary>
public static class ButtonIcons
{
    /// <summary>A small thread icon: a shaft with a helical groove on it.</summary>
    public static object? ThreadIcon()
    {
        try
        {
            using Bitmap bitmap = new(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using Pen shaft = new(Color.FromArgb(70, 70, 70), 5f);
                using Pen groove = new(Color.FromArgb(200, 40, 40), 3f);

                // The shaft, seen from the side.
                g.DrawLine(shaft, 6, 22, 26, 22);
                g.DrawLine(shaft, 6, 10, 26, 10);

                // The helical groove, drawn as the zig-zag it makes on a side view.
                g.DrawLine(groove, 7, 16, 12, 11);
                g.DrawLine(groove, 12, 11, 17, 21);
                g.DrawLine(groove, 17, 21, 22, 11);
                g.DrawLine(groove, 22, 11, 26, 15);
            }

            return PictureConverter.ToPicture(bitmap);
        }
        catch (Exception)
        {
            // An icon is a nicety: the button works without one.
            return null;
        }
    }

    /// <summary>
    /// The one public route to Windows Forms' own image-to-picture conversion:
    /// AxHost does the work, so no COM interop of our own is involved.
    /// </summary>
    private sealed class PictureConverter : AxHost
    {
        private PictureConverter()
            : base(string.Empty)
        {
        }

        public static object? ToPicture(Image image) => GetIPictureDispFromPicture(image);
    }
}
