using System.Runtime.InteropServices;
using System.Windows.Forms;
using WpfPoint = System.Windows.Point;

namespace CtrlHanabi.Services;

public sealed class WindowsCursorService : ICursorService
{
    public WpfPoint GetCursorScreenPoint()
    {
        if (GetPhysicalCursorPos(out var point) || GetCursorPos(out point))
        {
            return new WpfPoint(point.X, point.Y);
        }

        var fallback = Cursor.Position;
        return new WpfPoint(fallback.X, fallback.Y);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalCursorPos(out CursorPoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
