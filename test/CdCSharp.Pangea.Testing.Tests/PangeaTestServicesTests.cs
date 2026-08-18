using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Testing.Tests;

/// <summary>
/// The claim the package makes: a view model can be built and driven with no application around
/// it. These tests are that claim, written as a view model.
/// </summary>
public class PangeaTestServicesTests
{
    /// <summary>Carries an order, and names the screen that opens it.</summary>
    public sealed record ShowOrder(string Reference) : INavigationRequest<DetailViewModel>;

    public sealed class DetailViewModel(IServiceProvider services) : ViewModelBase(services);

    /// <summary>
    /// A view model of the shape the toolkit encourages: commands, dialogs, navigation. Written
    /// without <c>[Binding]</c> because the generator is not what is under test here.
    /// </summary>
    public class OrderViewModel : ViewModelBase
    {
        private readonly INavigationService _navigation;
        private readonly IDialogService _dialogs;

        public OrderViewModel(IServiceProvider services) : base(services)
        {
            _navigation = services.GetRequiredService<INavigationService>();
            _dialogs = services.GetRequiredService<IDialogService>();
        }

        public string Reference { get; set; } = "ORD-0001";

        public bool Deleted { get; private set; }

        public RelayCommand OpenCommand =>
            CreateCommand(() => _navigation.NavigateToAsync(new ShowOrder(Reference)));

        public RelayCommand DeleteCommand => CreateCommand(DeleteAsync);

        private async Task DeleteAsync()
        {
            if (!await _dialogs.ConfirmAsync("Delete", $"Delete {Reference}?")) return;

            Deleted = true;
        }
    }

    [Fact]
    public void AViewModel_IsBuiltFromTheTestServicesAlone()
    {
        PangeaTestServices services = new();

        OrderViewModel screen = new(services);

        Assert.Equal("ORD-0001", screen.Reference);
    }

    [Fact]
    public void ACommandThatNavigates_RecordsWhereItWentAndWhatItCarried()
    {
        PangeaTestServices services = new();
        OrderViewModel screen = new(services);

        screen.OpenCommand.Execute(null);

        Assert.Equal(typeof(DetailViewModel), services.Navigation.LastDestination);
        Assert.Equal("ORD-0001", services.Navigation.LastRequest<ShowOrder>()?.Reference);
    }

    [Fact]
    public void ACommandThatAsksFirst_ActsOnTheAnswerItWasGiven()
    {
        PangeaTestServices services = new();
        services.Dialogs.Answering(true);

        OrderViewModel screen = new(services);
        screen.DeleteCommand.Execute(null);

        Assert.True(screen.Deleted);
        Assert.Equal("Delete ORD-0001?", Assert.Single(services.Dialogs.Confirmations).Message);
    }

    [Fact]
    public void ACommandThatAsksFirst_DoesNothingWhenTheAnswerIsNo()
    {
        PangeaTestServices services = new();
        services.Dialogs.Answering(false);

        OrderViewModel screen = new(services);
        screen.DeleteCommand.Execute(null);

        Assert.False(screen.Deleted);
    }

    [Fact]
    public void TheApplicationsOwnServices_AreRegisteredAlongsideTheDoubles()
    {
        PangeaTestServices services = new();
        services.Add<IStorageService>(new InMemoryStorageService());

        Assert.NotNull(services.GetService(typeof(IStorageService)));
        Assert.NotNull(services.GetService(typeof(IUIDispatcher)));
    }

    /// <summary>
    /// Null rather than a throw, so <c>GetRequiredService</c> is what names the missing type.
    /// </summary>
    [Fact]
    public void AServiceThatWasNeverRegistered_ComesBackAsNull()
    {
        Assert.Null(new PangeaTestServices().GetService(typeof(IComparable)));
    }
}
