using Avalonia.Controls;

namespace CdCSharp.Pangea.Windows;

public interface IMainWindowManager
{
    Window? GetMainWindow();
    void SetMainWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class;
    void SetMainWindow(Window window);
}