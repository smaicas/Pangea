using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// What a view model does about the two things an application otherwise does by hand: a command
/// that failed, and a subscription that outlives the screen that made it.
/// </summary>
public class ViewModelLifecycleTests
{
    private sealed class RecordingLogger : ILogger, ILoggerFactory
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider) { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));

        public void Dispose() { }
    }

    private sealed class StubServices(IRelayCommandFactory factory, ILoggerFactory? logging, IUIDispatcher? dispatcher)
        : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRelayCommandFactory)) return factory;
            if (serviceType == typeof(ILoggerFactory)) return logging;
            if (serviceType == typeof(IUIDispatcher)) return dispatcher;

            return null;
        }
    }

    private sealed class Screen : ViewModelBase
    {
        public Screen(IServiceProvider services) : base(services) { }

        public TaskCompletionSource Gate { get; } = new();

        public Exception? Failure { get; set; }

        public List<Exception> Reported { get; } = [];

        public int Discards { get; private set; }

        public RelayCommand SaveCommand => CreateCommand(SaveAsync);

        public RelayCommand DeleteCommand => CreateCommand(() => { });

        public RelayCommand FailingCommand => CreateCommand(() => throw Failure ?? new InvalidOperationException("boom"));

        public new IDisposable Subscribe(
            Action<EventHandler> subscribe, Action<EventHandler> unsubscribe, EventHandler handler) =>
            base.Subscribe(subscribe, unsubscribe, handler);

        public new IDisposable Track(IDisposable subscription) => base.Track(subscription);

        private async Task SaveAsync() => await Gate.Task;

        protected override void OnCommandError(Exception ex)
        {
            Reported.Add(ex);
            base.OnCommandError(ex);
        }

        protected override void OnDiscarded() => Discards++;
    }

    private static Screen Create(out RecordingLogger logger)
    {
        logger = new RecordingLogger();

        return new Screen(new StubServices(
            new RelayCommandFactory(new FakeUIDispatcher()), logger, new FakeUIDispatcher()));
    }

    /// <summary>
    /// The bug this closes: a command body throws, nothing catches it, and the button reads as
    /// doing nothing at all. Nobody chooses to swallow exceptions - it was the default.
    /// </summary>
    [Fact]
    public void ACommandThatThrows_IsLoggedWithoutTheViewModelDoingAnything()
    {
        Screen screen = Create(out RecordingLogger logger);

        screen.FailingCommand.Execute(null);

        (LogLevel level, string message, Exception? exception) = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, level);
        Assert.Contains(nameof(Screen), message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void AnOverrideStillSeesTheFailure()
    {
        Screen screen = Create(out _);

        screen.FailingCommand.Execute(null);

        Assert.Single(screen.Reported);
    }

    /// <summary>
    /// Already true before any of this, and asserted because an application that does not believe
    /// it writes a busy flag of its own: a command refuses to run while it is running.
    /// </summary>
    [Fact]
    public async Task ACommandWillNotRunTwiceAtOnce()
    {
        Screen screen = Create(out _);

        Task running = screen.SaveCommand.ExecuteAsync();

        Assert.True(screen.SaveCommand.IsExecuting);
        Assert.False(screen.SaveCommand.CanExecute(null));

        // The second press, while the first is still going.
        screen.SaveCommand.Execute(null);

        screen.Gate.SetResult();
        await running;

        Assert.False(screen.SaveCommand.IsExecuting);
    }

    [Fact]
    public async Task IsBusy_FollowsTheCommandsAndTellsTheView()
    {
        Screen screen = Create(out _);

        List<string?> changes = [];
        screen.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Assert.False(screen.IsBusy);

        Task running = screen.SaveCommand.ExecuteAsync();

        Assert.True(screen.IsBusy);
        Assert.Contains(nameof(ViewModelBase.IsBusy), changes);

        screen.Gate.SetResult();
        await running;

        Assert.False(screen.IsBusy);

        // Once on the way in and once on the way out, and nothing in between.
        Assert.Equal(2, changes.Count(name => name == nameof(ViewModelBase.IsBusy)));
    }

    /// <summary>
    /// The half a busy flag is really wanted for: everything on the screen that is not the button
    /// that started the work.
    /// </summary>
    [Fact]
    public async Task WhileOneCommandRuns_TheOthersAreAskedAgain()
    {
        Screen screen = Create(out _);

        int asked = 0;
        screen.DeleteCommand.CanExecuteChanged += (_, _) => asked++;

        Task running = screen.SaveCommand.ExecuteAsync();

        Assert.True(asked > 0, "A command gated on IsBusy has no other way of hearing that work started.");

        screen.Gate.SetResult();
        await running;
    }

    [Fact]
    public void ASubscriptionIsReleasedWhenTheViewModelIsDiscarded()
    {
        Screen screen = Create(out _);
        Publisher publisher = new();

        screen.Subscribe(
            handler => publisher.Changed += handler,
            handler => publisher.Changed -= handler,
            (_, _) => screen.Reported.Add(new InvalidOperationException("heard")));

        publisher.Raise();
        Assert.Single(screen.Reported);

        screen.Discard();
        publisher.Raise();

        Assert.Single(screen.Reported);
        Assert.False(publisher.HasSubscribers, "The publisher is what would have kept the screen alive.");
    }

    [Fact]
    public void DiscardingTwice_DoesTheWorkOnce()
    {
        Screen screen = Create(out _);

        screen.Discard();
        screen.Discard();

        Assert.Equal(1, screen.Discards);
    }

    /// <summary>
    /// Subscribing after the discard would be a subscription nothing is ever going to release,
    /// which is the leak this whole mechanism exists to prevent.
    /// </summary>
    [Fact]
    public void SubscribingAfterTheDiscard_DoesNotSubscribe()
    {
        Screen screen = Create(out _);
        Publisher publisher = new();

        screen.Discard();

        screen.Subscribe(
            handler => publisher.Changed += handler,
            handler => publisher.Changed -= handler,
            (_, _) => screen.Reported.Add(new InvalidOperationException("heard")));

        publisher.Raise();

        Assert.Empty(screen.Reported);
        Assert.False(publisher.HasSubscribers);
    }

    [Fact]
    public void ASubscriptionCanBeReleasedOnItsOwn_AndIsNotReleasedTwice()
    {
        Screen screen = Create(out _);

        int releases = 0;
        IDisposable handle = screen.Track(new Releasable(() => releases++));

        handle.Dispose();
        handle.Dispose();
        screen.Discard();

        Assert.Equal(1, releases);
    }

    [Fact]
    public void WhatWasTrackedIsReleasedOnDiscard()
    {
        Screen screen = Create(out _);

        int releases = 0;
        screen.Track(new Releasable(() => releases++));

        screen.Discard();

        Assert.Equal(1, releases);
    }

    private sealed class Publisher
    {
        public event EventHandler? Changed;

        public bool HasSubscribers => Changed is not null;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Releasable(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
