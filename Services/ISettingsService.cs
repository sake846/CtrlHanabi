using CtrlHanabi.Models;

namespace CtrlHanabi.Services;

public interface ISettingsService
{
    HanabiSettings Load();
    void Save(HanabiSettings settings);
}
