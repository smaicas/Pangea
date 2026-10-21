namespace CdCSharp.Pangea.Apps.Tests;

[AppMessage("tests.echo")]
public sealed record Echo(string Text) : IAppRequest<EchoReply>
{
    public int Times { get; init; } = 1;
}

public sealed record EchoReply(string Text, string Caller);

[AppMessage("tests.fail")]
public sealed record Fail(string Reason) : IAppRequest<bool>;

[AppMessage("tests.wait")]
public sealed record WaitForever : IAppRequest<bool>;

[AppMessage("tests.nobody")]
public sealed record Nobody : IAppRequest<bool>;

[AppMessage("tests.happened")]
public sealed record Happened(string What) : IAppEvent;

/// <summary>Handled by a generated registration, so the manifest test has something real to read.</summary>
[AppMessage("tests.generated.ping")]
public sealed record GeneratedPing : IAppRequest<string>;

internal sealed class EchoHandler : IAppRequestHandler<Echo, EchoReply>
{
    public Task<EchoReply> HandleAsync(Echo request, AppRequestContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new EchoReply(string.Concat(Enumerable.Repeat(request.Text, request.Times)), context.CallerId));
}

internal sealed class FailHandler : IAppRequestHandler<Fail, bool>
{
    public Task<bool> HandleAsync(Fail request, AppRequestContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(request.Reason);
}

/// <summary>Never answers; records that it was cancelled.</summary>
internal sealed class WaitForeverHandler(WaitProbe probe) : IAppRequestHandler<WaitForever, bool>
{
    public async Task<bool> HandleAsync(WaitForever request, AppRequestContext context, CancellationToken cancellationToken)
    {
        probe.Started.TrySetResult();

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            probe.Cancelled.TrySetResult();
            throw;
        }
    }
}

public sealed class WaitProbe
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class GeneratedPingHandler : IAppRequestHandler<GeneratedPing, string>
{
    public Task<string> HandleAsync(GeneratedPing request, AppRequestContext context, CancellationToken cancellationToken) =>
        Task.FromResult("pong");
}
