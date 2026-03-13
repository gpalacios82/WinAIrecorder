using System.Drawing;
using System.Drawing.Drawing2D;

namespace VoiceType.Helpers;

public static class IconHelper
{
    /// <summary>
    /// Creates a microphone icon programmatically for use as the system tray icon.
    /// </summary>
    public static System.Drawing.Icon CreateMicrophoneIcon(int size = 32, System.Drawing.Color? color = null)
    {
        var iconColor = color ?? System.Drawing.Color.FromArgb(255, 99, 179, 237); // Blue accent
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);

        float s = size;

        // Microphone body (rounded rectangle)
        float bodyWidth = s * 0.38f;
        float bodyHeight = s * 0.46f;
        float bodyX = (s - bodyWidth) / 2;
        float bodyY = s * 0.06f;
        float radius = bodyWidth / 2;

        using var bodyBrush = new SolidBrush(iconColor);
        using var bodyPath = new GraphicsPath();
        bodyPath.AddArc(bodyX, bodyY, bodyWidth, bodyWidth, 180, 180);
        bodyPath.AddLine(bodyX + bodyWidth, bodyY + radius, bodyX + bodyWidth, bodyY + bodyHeight);
        bodyPath.AddArc(bodyX, bodyY + bodyHeight - bodyWidth, bodyWidth, bodyWidth, 0, 180);
        bodyPath.AddLine(bodyX, bodyY + bodyHeight, bodyX, bodyY + radius);
        bodyPath.CloseFigure();
        g.FillPath(bodyBrush, bodyPath);

        // Microphone stand (arc)
        using var standPen = new System.Drawing.Pen(iconColor, s * 0.07f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float arcSize = s * 0.6f;
        float arcX = (s - arcSize) / 2;
        float arcY = s * 0.3f;
        g.DrawArc(standPen, arcX, arcY, arcSize, arcSize * 0.8f, 0, 180);

        // Vertical stem
        float stemX = s / 2;
        float stemTopY = s * 0.7f;
        float stemBottomY = s * 0.88f;
        g.DrawLine(standPen, stemX, stemTopY, stemX, stemBottomY);

        // Horizontal base
        float baseWidth = s * 0.42f;
        float baseY = s * 0.88f;
        g.DrawLine(standPen, (s - baseWidth) / 2, baseY, (s + baseWidth) / 2, baseY);

        // Convert bitmap to icon
        IntPtr hIcon = bitmap.GetHicon();
        var icon = System.Drawing.Icon.FromHandle(hIcon);
        // Clone to own the handle
        var result = (System.Drawing.Icon)icon.Clone();
        DestroyIcon(hIcon);
        return result;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
