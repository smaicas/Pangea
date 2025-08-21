using Avalonia.Controls;
using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Windows;

public interface IWindowManagerCore
{
    Task<TWindow> ShowWindowAsync<TWindow, TViewModel>(NavigationParameter? navigationParameter = null)
        where TWindow : Window, new() where TViewModel : class;
    TWindow CreateWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class;
    TWindow CreateWindow<TWindow>() where TWindow : Window, new();
    void CloseWindow<TWindow>() where TWindow : Window;
    bool IsWindowOpen<TWindow>() where TWindow : Window;
    TWindow? GetWindow<TWindow>() where TWindow : Window;
}