using Avalonia.Platform.Storage;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;

namespace CdCSharp.Pangea.Platform;

/// <inheritdoc cref="ISystemLauncher"/>
/// <remarks>
/// Opening goes through Avalonia's launcher, which knows each platform's rules, while there is a
/// window or a view to ask; before there is one, desktop platforms are asked directly. Showing a file
/// selected has no Avalonia equivalent, so it is the file manager's own command on each desktop.
/// </remarks>
public sealed class SystemLauncher(IServiceProvider services, IUIDispatcher dispatcher, ILogger<SystemLauncher> logger) : ISystemLauncher
{
    public async Task<bool> OpenAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !(File.Exists(path) || Directory.Exists(path))) return false;

        string full = Path.GetFullPath(path);
        bool folder = Directory.Exists(full);

        bool? launched = await LaunchAsync(launcher => folder
            ? launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(full))
            : launcher.LaunchFileInfoAsync(new FileInfo(full)));

        return launched ?? OpenDirectly(full);
    }

    public async Task<bool> ShowInFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (Directory.Exists(path)) return await OpenAsync(path);
        if (!File.Exists(path)) return false;

        string full = Path.GetFullPath(path);

        // "/select," and the path apart: Explorer reads them as one instruction either way, and a
        // path with a comma or spaces survives being its own argument.
        if (OperatingSystem.IsWindows() && Run("explorer.exe", "/select,", full)) return true;
        if (OperatingSystem.IsMacOS() && Run("open", "-R", full)) return true;
        if (OperatingSystem.IsLinux() && await ShowWithFreedesktopAsync(full)) return true;

        return Path.GetDirectoryName(full) is { } folder && await OpenAsync(folder);
    }

    public async Task<bool> OpenUriAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        bool? launched = await LaunchAsync(launcher => launcher.LaunchUriAsync(uri));

        return launched ?? OpenDirectly(uri.AbsoluteUri);
    }

    /// <summary>Asks Avalonia's launcher, or answers <see langword="null"/> when there is none to ask yet.</summary>
    private Task<bool?> LaunchAsync(Func<ILauncher, Task<bool>> launch) => dispatcher.InvokeAsync<bool?>(async () =>
    {
        if (MainTopLevel.Find(services) is not { Launcher: { } launcher }) return null;

        try
        {
            return await launch(launcher);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            logger.LogInformation(ex, "The system would not open it");
            return false;
        }
    });

    /// <summary>The desktop's own way of opening a path or a link, for when there is no window yet.</summary>
    private bool OpenDirectly(string target)
    {
        if (OperatingSystem.IsWindows()) return Start(new ProcessStartInfo(target) { UseShellExecute = true });
        if (OperatingSystem.IsMacOS()) return Run("open", target);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD()) return Run("xdg-open", target);

        return false;
    }

    /// <summary>The freedesktop file manager interface, which Nautilus, Dolphin, Nemo and others answer.</summary>
    private async Task<bool> ShowWithFreedesktopAsync(string path)
    {
        ProcessStartInfo start = Command(
            "dbus-send", "--session", "--print-reply", "--dest=org.freedesktop.FileManager1", "--type=method_call",
            "/org/freedesktop/FileManager1", "org.freedesktop.FileManager1.ShowItems",
            $"array:string:{new Uri(path).AbsoluteUri}", "string:");

        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;

        try
        {
            using Process? process = Process.Start(start);
            if (process is null) return false;

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or OperationCanceledException)
        {
            logger.LogInformation(ex, "No file manager answered on the session bus");
            return false;
        }
    }

    private bool Run(string file, params string[] arguments) => Start(Command(file, arguments));

    private static ProcessStartInfo Command(string file, params string[] arguments)
    {
        ProcessStartInfo start = new(file) { UseShellExecute = false };

        foreach (string argument in arguments) start.ArgumentList.Add(argument);

        return start;
    }

    private bool Start(ProcessStartInfo start)
    {
        try
        {
            using Process? process = Process.Start(start);
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            logger.LogInformation(ex, "Could not run {Command}", start.FileName);
            return false;
        }
    }
}
