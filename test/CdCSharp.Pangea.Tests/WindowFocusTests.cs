using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Testing.Dispatchers;
using CdCSharp.Pangea.Tests.Infrastructure;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Where keyboard focus lands when the window manager opens a window.
/// </summary>
/// <remarks>
/// A window that opens with nothing focused leaves the keyboard with no starting point. The rule is
/// deliberately narrow: focus the first control only when the window focused nothing itself, so an
/// application that knows where focus belongs keeps deciding.
/// </remarks>
public class WindowFocusTests
{
    public sealed class FormWindow : Window
    {
        public FormWindow()
        {
            Width = 300;
            Height = 200;

            StackPanel panel = new();
            panel.Children.Add(Box = new TextBox());
            panel.Children.Add(new Button { Content = "ok" });
            Content = panel;
        }

        public TextBox Box { get; }
    }

    /// <summary>Decides for itself, which this must not override.</summary>
    public sealed class OpinionatedWindow : Window
    {
        public OpinionatedWindow()
        {
            Width = 300;
            Height = 200;

            StackPanel panel = new();
            panel.Children.Add(new TextBox());
            panel.Children.Add(Preferred = new Button { Content = "the one it wants" });
            Content = panel;

            Opened += (_, _) => Preferred.Focus();
        }

        public Button Preferred { get; }
    }

    public sealed class NothingFocusableWindow : Window
    {
        public NothingFocusableWindow()
        {
            Width = 300;
            Height = 200;
            Content = new TextBlock { Text = "read only" };
        }
    }

    public sealed class AnyViewModel;

    private static WindowManager Create() =>
        new(new StubServices(),
            Options.Create(new PangeaOptions { Window = { AutoDiscoverMainWindow = false } }),
            new TypeRegistry(),
            new PumpingUIDispatcher(),
            NullLogger<WindowManager>.Instance);

    private static void Pump()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    [AvaloniaFact]
    public async Task AWindowThatOpensWithNoFocus_GetsItsFirstControlFocused()
    {
        WindowManager windows = Create();

        FormWindow window = await windows.ShowWindowAsync<FormWindow, AnyViewModel>();
        window.Activate();
        Pump();

        Assert.Same(window.Box, window.FocusManager?.GetFocusedElement());
        window.Close();
    }

    /// <summary>
    /// The narrow part of the rule: a window that placed focus itself keeps it.
    /// </summary>
    [AvaloniaFact]
    public async Task AWindowThatFocusesSomethingItself_IsLeftAlone()
    {
        WindowManager windows = Create();

        OpinionatedWindow window = await windows.ShowWindowAsync<OpinionatedWindow, AnyViewModel>();
        window.Activate();
        Pump();

        Assert.Same(window.Preferred, window.FocusManager?.GetFocusedElement());
        window.Close();
    }

    [AvaloniaFact]
    public async Task AWindowWithNothingToFocus_IsNotAProblem()
    {
        WindowManager windows = Create();

        NothingFocusableWindow window = await windows.ShowWindowAsync<NothingFocusableWindow, AnyViewModel>();
        window.Activate();
        Pump();

        // Nothing to focus and nothing thrown; the window is simply read-only.
        Assert.Null(window.FocusManager?.GetFocusedElement() as InputElement);
        window.Close();
    }
}
