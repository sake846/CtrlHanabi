using WpfPoint = System.Windows.Point;

namespace CtrlHanabi.Services;

public interface ICursorService
{
    WpfPoint GetCursorScreenPoint();
}
