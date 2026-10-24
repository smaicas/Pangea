using CdCSharp.Pangea.Apps.Testing;
using CdCSharp.Pangea.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// Work that must not be cut short keeps the application open, and what another application asks
/// the person to do waits its turn, ends with an answer however it ends, and is never left hanging.
/// </summary>
public class InteractionTests
{
    private sealed record EditResult(string? FilePath, bool Cancelled);

    private sealed class EditSession(string filePath, string requester)
        : AppInteraction<EditResult>(requester, new EditResult(null, Cancelled: true))
    {
        public string FilePath { get; } = filePath;
    }

    private static Task<bool> AsksToCloseAsync(AppActivity activity) =>
        new AppActivityCloseHandler(activity, NullLogger<AppActivityCloseHandler>.Instance)
            .OnCloseRequestedAsync("update", CancellationToken.None);

    [Fact]
    public async Task Work_going_on_keeps_the_application_open_until_it_ends()
    {
        AppActivity activity = new();
        int changes = 0;
        activity.Changed += (_, _) => changes++;

        IDisposable export = activity.Begin("Exporting the video");

        Assert.True(activity.IsBusy);
        Assert.Equal(["Exporting the video"], activity.Running);
        Assert.False(await AsksToCloseAsync(activity));

        export.Dispose();
        export.Dispose();

        Assert.False(activity.IsBusy);
        Assert.True(await AsksToCloseAsync(activity));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void The_feature_registers_the_close_handler_beside_the_applications_own()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IAppCloseHandler, NeverCloses>();
        new AppsFeature().ConfigureServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();

        IAppCloseHandler[] handlers = provider.GetServices<IAppCloseHandler>().ToArray();

        Assert.Equal(2, handlers.Length);
        Assert.Contains(handlers, handler => handler is AppActivityCloseHandler);
        Assert.NotNull(provider.GetService<AppInteractions>());
        Assert.NotNull(provider.GetService<AppRequestFolders>());
    }

    [Fact]
    public async Task An_interaction_is_current_until_the_person_answers()
    {
        AppInteractions interactions = Interactions(out AppActivity activity);
        EditSession session = new("photo.png", "Diskord");
        AppInteraction? shown = null;

        Task<EditResult> answer = interactions.RunAsync(session, () =>
        {
            shown = interactions.Current;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Same(session, shown);
        Assert.True(activity.IsBusy);
        Assert.False(answer.IsCompleted);

        session.Complete(new EditResult("edited.png", Cancelled: false));

        Assert.Equal("edited.png", (await answer).FilePath);
        Assert.Null(interactions.Current);
        Assert.False(activity.IsBusy);
    }

    [Fact]
    public async Task A_second_request_waits_for_the_first_rather_than_replacing_it()
    {
        AppInteractions interactions = Interactions(out _);
        EditSession first = new("one.png", "Diskord");
        EditSession second = new("two.png", "Automatik");
        List<string> opened = [];

        Task<EditResult> firstAnswer = interactions.RunAsync(first, () => Open(first), TestContext.Current.CancellationToken);
        Task<EditResult> secondAnswer = interactions.RunAsync(second, () => Open(second), TestContext.Current.CancellationToken);

        Assert.Equal(["one.png"], opened);
        Assert.Equal(1, interactions.Waiting);

        first.Complete(new EditResult("one-edited.png", false));
        await firstAnswer;
        await WaitUntilAsync(() => interactions.Current == second);

        Assert.Equal(["one.png", "two.png"], opened);

        interactions.CancelCurrent();

        Assert.True((await secondAnswer).Cancelled);

        Task Open(EditSession session)
        {
            opened.Add(session.FilePath);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task The_caller_giving_up_ends_it_with_the_cancelled_answer()
    {
        AppInteractions interactions = Interactions(out _);
        EditSession session = new("photo.png", "Diskord");
        using CancellationTokenSource caller = new();

        Task<EditResult> answer = interactions.RunAsync(session, () => Task.CompletedTask, caller.Token);
        await caller.CancelAsync();

        Assert.True((await answer).Cancelled);
        Assert.False(session.IsOpen);
        Assert.Null(interactions.Current);
    }

    [Fact]
    public async Task A_screen_that_fails_to_open_cancels_it_and_says_why()
    {
        AppInteractions interactions = Interactions(out AppActivity activity);
        EditSession session = new("photo.png", "Diskord");

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interactions.RunAsync(session, () => throw new InvalidOperationException("The file is not an image."), TestContext.Current.CancellationToken));

        Assert.Equal("The file is not an image.", failure.Message);
        Assert.True((await session.Result).Cancelled);
        Assert.Null(interactions.Current);
        Assert.False(activity.IsBusy);
    }

    private static AppInteractions Interactions(out AppActivity activity)
    {
        PangeaTestServices services = new PangeaTestServices().AddApps();
        activity = services.GetRequiredService<AppActivity>();
        return services.GetRequiredService<AppInteractions>();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class NeverCloses : IAppCloseHandler
    {
        public Task<bool> OnCloseRequestedAsync(string reason, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
