namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Answers one kind of request in the application that declares it.
/// </summary>
/// <remarks>
/// <para>
/// Declaring the class is all there is to it: the toolkit's generator finds it, registers it, and
/// lists the message id in the application's manifest so a host knows who to start for it. The
/// handler is built from the container for each request, so ask for services in the constructor.
/// </para>
/// <para>
/// An exception thrown here travels back to the sender as an <see cref="AppRequestFailedException"/>
/// carrying its message. Write that message for the person who will read it in the other
/// application.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request, which must carry <see cref="AppMessageAttribute"/>.</typeparam>
/// <typeparam name="TResponse">What the sender receives.</typeparam>
public interface IAppRequestHandler<in TRequest, TResponse>
    where TRequest : IAppRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, AppRequestContext context, CancellationToken cancellationToken);
}

/// <summary>Who asked, for a handler that wants to say so.</summary>
/// <param name="CallerId">The id of the application that sent the request.</param>
public sealed record AppRequestContext(string CallerId);
