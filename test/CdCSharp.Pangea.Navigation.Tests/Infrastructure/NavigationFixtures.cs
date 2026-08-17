using Avalonia.Controls;
using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Navigation.Tests.Infrastructure;

public sealed record ShowOrder(Guid Id) : INavigationRequest<OrderViewModel>;

public sealed record ShowReport(string Title) : INavigationRequest<ReportViewModel>;

/// <summary>Accepts a request, and records what it was handed.</summary>
public class OrderViewModel : INavigationAware, INavigationAware<ShowOrder>
{
    public List<string> Calls { get; } = [];

    public Guid? ReceivedId { get; private set; }

    public bool AllowLeaving { get; set; } = true;

    public Task OnNavigatedToAsync(ShowOrder request)
    {
        Calls.Add("arrived-with-request");
        ReceivedId = request.Id;
        return Task.CompletedTask;
    }

    public Task OnNavigatedToAsync()
    {
        Calls.Add("arrived");
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync()
    {
        Calls.Add("left");
        return Task.CompletedTask;
    }

    public Task<bool> CanNavigateAwayAsync()
    {
        Calls.Add("asked-to-leave");
        return Task.FromResult(AllowLeaving);
    }
}

/// <summary>Takes no request; only the parameterless hook applies.</summary>
public class ReportViewModel : INavigationAware, INavigationAware<ShowReport>
{
    public List<string> Calls { get; } = [];

    public string? ReceivedTitle { get; private set; }

    public Task OnNavigatedToAsync(ShowReport request)
    {
        Calls.Add("arrived-with-request");
        ReceivedTitle = request.Title;
        return Task.CompletedTask;
    }

    public Task OnNavigatedToAsync()
    {
        Calls.Add("arrived");
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync()
    {
        Calls.Add("left");
        return Task.CompletedTask;
    }

    public Task<bool> CanNavigateAwayAsync() => Task.FromResult(true);
}

/// <summary>Implements nothing: navigating to it must not require the hooks.</summary>
public class PlainViewModel;

/// <summary>Records which thread its arrival hook ran on.</summary>
public class ThreadRecordingViewModel : INavigationAware
{
    public int? HookThreadId { get; private set; }

    public Task OnNavigatedToAsync()
    {
        HookThreadId = Environment.CurrentManagedThreadId;
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    public Task<bool> CanNavigateAwayAsync() => Task.FromResult(true);
}

public class OrderView : ContentControl;

public class ReportView : ContentControl;

/// <summary>Resolves view models by constructing them, the way the container would.</summary>
public sealed class StubServices : IServiceProvider
{
    private readonly Dictionary<Type, object> _singletons = [];

    public object? GetService(Type serviceType)
    {
        if (_singletons.TryGetValue(serviceType, out object? existing)) return existing;

        object created = Activator.CreateInstance(serviceType)!;
        _singletons[serviceType] = created;
        return created;
    }
}
