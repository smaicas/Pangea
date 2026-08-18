using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Data.CodeAnalysis.Tests;

/// <summary>
/// The rule that catches a context registered with no engine, before the container is built and
/// throws for it.
/// </summary>
public class NoProviderTests
{
    private const string Id = "PGD001";

    [Fact]
    public void ARegistrationThatNamesNoEngine_IsReported()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(Id, """
            public static class Registration
            {
                public static void Register(IServiceCollection services) =>
                    services.AddPangeaDbContext<AppDbContext>(db => db.Options.DatabaseFileName = "app.db");
            }
            """);

        Diagnostic diagnostic = Assert.Single(reported);

        // The message has to name the package, because the fix is an install rather than an edit.
        Assert.Contains("AppDbContext", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("CdCSharp.Pangea.Data.Sqlite", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARegistrationThatChoosesOne_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public static class Registration
            {
                public static void Register(IServiceCollection services) =>
                    services.AddPangeaDbContext<AppDbContext>(db =>
                    {
                        db.UseSqlite("app.db");
                        db.Options.BackupsToKeep = 2;
                    });
            }
            """));
    }

    /// <summary>
    /// Any provider, not a list of method names the feature would have to be told about: the rule
    /// asks whether something in the callback returned the builder.
    /// </summary>
    [Fact]
    public void AProviderChosenThroughUseProvider_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public static class Registration
            {
                public static void Register(IServiceCollection services, IPangeaDbProvider provider) =>
                    services.AddPangeaDbContext<AppDbContext>(db => db.UseProvider(provider));
            }
            """));
    }

    /// <summary>
    /// A callback written somewhere else is configuration the analyzer cannot see through, and
    /// guessing would report the applications that factored their registration out.
    /// </summary>
    [Fact]
    public void ACallbackItCannotSeeThrough_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public static class Registration
            {
                public static void Register(IServiceCollection services) =>
                    services.AddPangeaDbContext<AppDbContext>(Configure);

                private static void Configure(PangeaDbBuilder db) => db.UseSqlite();
            }
            """));
    }
}

/// <summary>
/// The rule that catches a context taken out of the container, which is the mistake the feature is
/// shaped to prevent and the one with the longest delay before it hurts.
/// </summary>
public class ContextResolutionTests
{
    private const string Id = "PGD002";

    [Fact]
    public void ResolvingTheContext_IsReported()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(Id, """
            public class Screen
            {
                private readonly AppDbContext _context;

                public Screen(IServiceProvider services) => _context = services.GetRequiredService<AppDbContext>();
            }
            """);

        Assert.Contains("IPangeaDbContext<AppDbContext>", Assert.Single(reported).GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AskingForItOptionally_IsReportedToo()
    {
        Assert.Single(AnalyzerTestHelper.Run(Id, """
            public class Screen
            {
                public Screen(IServiceProvider services) => services.GetService<AppDbContext>();
            }
            """));
    }

    /// <summary>
    /// An application may use this feature for one database and Entity Framework's own registration
    /// for another. A context it registered itself is a context it is entitled to resolve, and
    /// reporting that would be the analyzer overruling a decision it cannot see the reason for.
    /// </summary>
    [Fact]
    public void ResolvingAContextTheApplicationRegisteredItself_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public static class Registration
            {
                public static void Register(IServiceCollection services) =>
                    services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=own.db"));

                public static AppDbContext Resolve(IServiceProvider services) =>
                    services.GetRequiredService<AppDbContext>();
            }
            """));
    }

    /// <summary>
    /// And the other half of that: one context registered each way, in one compilation. Only the
    /// one this feature owns is reported.
    /// </summary>
    [Fact]
    public void WithOneContextEachWay_OnlyTheFeaturesOwnIsReported()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(Id, """
            public class ArchiveContext : DbContext
            {
                public ArchiveContext(DbContextOptions<ArchiveContext> options) : base(options) { }
            }

            public static class Registration
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddPangeaDbContext<AppDbContext>(db => db.UseSqlite());
                    services.AddDbContext<ArchiveContext>(options => options.UseSqlite("Data Source=archive.db"));
                }

                public static void Resolve(IServiceProvider services)
                {
                    services.GetRequiredService<ArchiveContext>();
                    services.GetRequiredService<AppDbContext>();
                }
            }
            """);

        Assert.Contains("AppDbContext", Assert.Single(reported).GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvingWhatTheApplicationShouldAskFor_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public class Screen
            {
                private readonly IPangeaDbContext<AppDbContext> _db;

                public Screen(IServiceProvider services) =>
                    _db = services.GetRequiredService<IPangeaDbContext<AppDbContext>>();
            }
            """));
    }
}

/// <summary>
/// The rule that catches a save inside a write. Harmless, and invisible: the second save finds
/// nothing to do, so nothing ever says the code means less than it looks like it does.
/// </summary>
public class RedundantSaveTests
{
    private const string Id = "PGD003";

    [Fact]
    public void SavingInsideWriteAsync_IsReported()
    {
        Assert.Single(AnalyzerTestHelper.Run(Id, """
            public class Screen
            {
                public Task Add(IPangeaDbContext<AppDbContext> db) => db.WriteAsync(async (context, token) =>
                {
                    context.Notes.Add(new Note());
                    await context.SaveChangesAsync(token);
                });
            }
            """));
    }

    [Fact]
    public void WritingWithoutSaving_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public class Screen
            {
                public Task Add(IPangeaDbContext<AppDbContext> db) => db.WriteAsync((context, token) =>
                {
                    context.Notes.Add(new Note());
                    return Task.CompletedTask;
                });
            }
            """));
    }

    /// <summary>
    /// A context the caller built is a context the caller saves. Only the callback that already
    /// has a save after it is worth reporting.
    /// </summary>
    [Fact]
    public void SavingAContextOfYourOwn_IsQuiet()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Id, """
            public class Screen
            {
                public async Task Add(IPangeaDbContext<AppDbContext> db)
                {
                    await using AppDbContext context = db.Create();

                    context.Notes.Add(new Note());
                    await context.SaveChangesAsync();
                }
            }
            """));
    }
}
