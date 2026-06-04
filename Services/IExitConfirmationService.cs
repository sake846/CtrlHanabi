using System.Windows.Forms;

namespace CtrlHanabi.Services;

public interface IExitConfirmationService
{
    DialogResult ConfirmExit(string appName, string message);
}
