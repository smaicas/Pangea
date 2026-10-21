using CdCSharp.Pangea.Apps.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// What a host learns about an application before running it, read from an assembly the generator
/// really processed: this one.
/// </summary>
public class ManifestTests
{
    private static string ThisAssembly => typeof(ManifestTests).Assembly.Location;

    [Fact]
    public void The_manifest_is_read_from_metadata()
    {
        AppManifest manifest = Assert.IsType<AppManifest>(AppManifestReader.TryRead(ThisAssembly));

        Assert.Equal("pangea-tests", manifest.Id);
        Assert.Equal("Pangea Tests", manifest.Name);
        Assert.Equal("The test assembly, described the way an application is.", manifest.Description);
        Assert.NotNull(manifest.Version);
    }

    [Fact]
    public void Every_declared_handler_is_listed_so_a_host_knows_who_to_start()
    {
        AppManifest manifest = AppManifestReader.TryRead(ThisAssembly)!;

        Assert.Equal(["tests.echo", "tests.fail", "tests.generated.ping", "tests.wait"], manifest.Handles);
    }

    [Fact]
    public void An_assembly_that_is_not_an_application_has_no_manifest()
    {
        Assert.Null(AppManifestReader.TryRead(typeof(AppManifestReader).Assembly.Location));
        Assert.Null(AppManifestReader.TryRead(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll")));
    }

    [Fact]
    public void The_generated_registrations_are_in_the_catalog()
    {
        Assert.Contains(AppHandlerCatalog.All, entry => entry.MessageId == "tests.generated.ping" && entry.HandlerType == typeof(GeneratedPingHandler));
    }

    [Fact]
    public async Task An_application_answers_what_the_generator_registered()
    {
        await using BusHarness bus = new();
        await bus.ConnectAsync("generated", services => services.AddSingleton(new WaitProbe()), includeGenerated: true);
        AppBusClient sender = await bus.ConnectAsync("sender");

        Assert.Equal("pong", await sender.SendAsync(new GeneratedPing(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("1.2.3+0123456789abcdef", "1.2.3", "0123456789abcdef")]
    [InlineData("1.2.3", "1.2.3", null)]
    [InlineData("1.2.3+", "1.2.3", null)]
    [InlineData(null, null, null)]
    public void The_commit_is_split_from_the_version(string? informational, string? version, string? commit)
    {
        Assert.Equal((version, commit), AppManifestReader.SplitVersion(informational));
        Assert.Equal((version, commit), AppIdentity.SplitVersion(informational));
    }
}
