# Supabase

A shared Postgres backend for the application, installed separately:

```bash
dotnet add package CdCSharp.Pangea.Supabase
```

Use it when more than one person has to see the same data. For an application whose data belongs to
one machine, `IStorageService` or `CdCSharp.Pangea.Data.Sqlite` is less to go wrong.

---

## The three rules

1. **Never inject `Supabase.Client`.** Inject `ISupabaseClientProvider`. A client that has not been
   initialized looks exactly like one that has, right up until the first request fails in a way that
   reads as a backend fault.
2. **Never put the `service_role` key in `AnonKey`.** It bypasses row level security, and anything
   shipped to a device can be read off it. What a request may do is decided by RLS policies against
   the signed-in user.
3. **Never assume the network is there.** A phone loses it constantly. Queue the write in `IOutbox`
   and replay it; refusing the write is what makes an application feel broken on a train.

---

## Configuration

```csharp
using CdCSharp.Pangea.Supabase;
using Microsoft.Extensions.DependencyInjection;

public static class BackendRegistration
{
    public static void Register(IServiceCollection services) =>
        services.Configure<SupabaseOptions>(options =>
        {
            options.Url = "https://<ref>.supabase.co";
            options.AnonKey = "<anon key>";

            // An account before the user has been asked for anything. It exists only as long as the
            // stored session does, which is why PersistSession stays on.
            options.SignInAnonymouslyOnStart = true;
        });
}
```

The feature registers itself by being present. Leaving `Url` or `AnonKey` unset fails at startup
naming the option, rather than as a connection error with no host or a 401 on the first query.

Startup connects behind the splash - `SupabaseInitializer`, order 100 - and by default a backend it
cannot reach is logged and startup carries on. Set `RequireConnectionAtStartup` only when the first
screen genuinely has nothing to draw without the server.

---

## Signing in

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Supabase.Abstractions;

public partial class AccountViewModel : ViewModelBase
{
    private readonly ISupabaseAuth _auth;

    public AccountViewModel(IServiceProvider services, ISupabaseAuth auth) : base(services) => _auth = auth;

    public string? UserId => _auth.UserId;

    // Worth saying somewhere quiet: an anonymous account lives in this installation's stored
    // session, so losing the device loses the data.
    public bool ShouldOfferToKeepTheAccount => _auth.IsAnonymous;

    public RelayCommand KeepCommand => CreateCommand(() => _auth.LinkEmailAsync("someone@example.com"));
}
```

`EnsureSignedInAsync` signs in anonymously and does nothing when there is already a session -
signing in over one would abandon the account it belongs to, which for an anonymous user means
abandoning everything they have. `LinkEmailAsync` keeps the same account and everything in it.

`SignOutAsync` on an anonymous account is a deletion, not a sign-out: nothing can reach that account
again. Ask first.

`Changed` is raised on whatever thread the change arrived on. Marshal through `IUIDispatcher` before
touching a view model.

---

## Querying

```csharp
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("expenses")]
public class ExpenseRow : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("group_id")] public Guid GroupId { get; set; }
    [Column("amount_cents")] public long AmountCents { get; set; }
}
```

```csharp
using CdCSharp.Pangea.Supabase.Abstractions;
using System.Threading;

public sealed class Expenses
{
    private readonly ISupabaseClientProvider _backend;

    public Expenses(ISupabaseClientProvider backend) => _backend = backend;

    public async Task<List<ExpenseRow>> OfGroupAsync(Guid groupId, CancellationToken token = default)
    {
        Supabase.Client client = await _backend.InitializeAsync(token);

        return (await client.From<ExpenseRow>().Where(row => row.GroupId == groupId).Get()).Models;
    }
}
```

`InitializeAsync` returns the client already built when there is one, so calling it per request
costs nothing and removes the question of whether startup finished.

---

## Writes made offline

```csharp
using CdCSharp.Pangea.Supabase.Abstractions;
using System.Text.Json;

public sealed class ExpenseWriter
{
    private readonly IOutbox _outbox;

    public ExpenseWriter(IOutbox outbox) => _outbox = outbox;

    public Task RecordAsync(ExpenseRow expense) =>
        _outbox.EnqueueAsync("expense.add", JsonSerializer.Serialize(expense));

    // On reconnect. Returning false leaves the entry queued and stops the drain: the order writes
    // were made in is usually the order they have to be applied in.
    public Task<int> FlushAsync(Func<ExpenseRow, Task<bool>> send) =>
        _outbox.DrainAsync(async (entry, token) => entry.Kind switch
        {
            "expense.add" => await send(JsonSerializer.Deserialize<ExpenseRow>(entry.Payload)!),
            _ => true   // a kind this build no longer knows about is dropped, not retried forever
        });
}
```

The queue is untyped on purpose: what a pending write means is the application's business. It
survives a restart, records an attempt count per entry, and treats an unreadable file as empty -
throwing from every write afterwards would turn one corrupt file into an application that cannot be
used, and the entries are gone either way.

---

## Where the session lives

`supabase-session.json` in the per-platform data directory, holding a refresh token. On Android and
iOS the sandbox protects it; on a desktop it is a file in the user's profile, readable by anything
running as that user. That is the platform's guarantee, not encryption - an application handling more
than its own user's data should sign in against something stronger than an anonymous account.

---

## Pitfalls

- **RLS is the security model.** A table with no policy is readable by nobody through the anon key,
  and a table with `using (true)` is readable by everybody. There is no middle state that happens by
  accident.
- **`SignOutAsync` deletes an anonymous account.** Confirm it.
- **The client is not trim safe.** It maps rows with reflection; the package says so, and publishing
  trimmed loses columns silently.
- **`Changed` is not on the UI thread.**
- **A drain stops at the first failure.** That is the contract, not a bug: skipping would apply a
  later write on top of a missing earlier one.
