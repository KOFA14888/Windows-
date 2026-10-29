using System.Windows.Controls;

namespace Win10Sim.App.Controls;

/// <summary>
/// Логотип Windows (четыре квадрата). Цвет задаётся свойством Foreground —
/// по умолчанию белый, чтобы контрол работал на тёмных экранах без настройки.
/// </summary>
public partial class WindowsLogo : UserControl
{
    public WindowsLogo()
    {
        InitializeComponent();
    }
}
