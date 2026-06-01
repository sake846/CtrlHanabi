using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CtrlHanabi.Services;

public sealed class WindowsExitConfirmationService : IExitConfirmationService
{
    private const uint MbIconQuestion = 0x00000020;
    private const uint MbYesNo = 0x00000004;
    private const uint MbSetForeground = 0x00010000;
    private const uint MbTopmost = 0x00040000;
    private const int IdYes = 6;
    private const string ExitConfirmMessage = "CtrlHanabiを終了しますか？";

    public DialogResult ConfirmExit(string appName)
    {
        var result = MessageBox(
            nint.Zero,
            ExitConfirmMessage,
            appName,
            MbYesNo | MbIconQuestion | MbSetForeground | MbTopmost);

        return result == IdYes ? DialogResult.Yes : DialogResult.No;
    }

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(nint hWnd, string lpText, string lpCaption, uint uType);
}
