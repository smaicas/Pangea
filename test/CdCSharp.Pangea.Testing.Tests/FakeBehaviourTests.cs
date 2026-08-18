using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Testing.Dispatchers;
using CdCSharp.Pangea.Testing.Fakes;
using System.ComponentModel;

namespace CdCSharp.Pangea.Testing.Tests;

/// <summary>
/// Each double behaves like the thing it stands in for where that matters, and is inspectable
/// where the real one is not. A double that quietly differs is worse than no double.
/// </summary>
public class FakeBehaviourTests
{
    public sealed class HomeViewModel;

    public sealed class SettingsViewModel;

    [Fact]
    public async Task TheDialogService_FallsBackToItsDefaultOnceTheScriptRunsOut()
    {
        RecordingDialogService dialogs = new() { DefaultAnswer = true };
        dialogs.Answering(false);

        Assert.False(await dialogs.ConfirmAsync("t", "m"));
        Assert.True(await dialogs.ConfirmAsync("t", "m"));
    }

    [Fact]
    public async Task TheDialogService_RemembersHowTheQuestionWasWorded()
    {
        RecordingDialogService dialogs = new();

        await dialogs.ConfirmAsync("Unsaved changes", "Discard them?", "Discard", "Stay");

        DialogRequest asked = Assert.Single(dialogs.Confirmations);

        Assert.Equal("Unsaved changes", asked.Title);
        Assert.Equal("Discard", asked.ConfirmText);
        Assert.Equal("Stay", asked.CancelText);
    }

    [Fact]
    public async Task TheNavigationService_ReportsRefusalTheWayTheRealOneDoes()
    {
        RecordingNavigationService navigation = new() { Refuse = true };

        Assert.False(await navigation.NavigateToAsync<HomeViewModel>());
        Assert.Single(navigation.Navigations);
    }

    /// <summary>
    /// History and <c>CanGoBack</c> follow what is showing, because a shell binds a button to it.
    /// </summary>
    [Fact]
    public async Task TheNavigationService_KeepsHistoryAsTheShownScreenChanges()
    {
        RecordingNavigationService navigation = new();
        List<string?> changed = [];
        ((INotifyPropertyChanged)navigation).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.False(navigation.CanGoBack);

        navigation.CurrentViewModel = new HomeViewModel();
        Assert.False(navigation.CanGoBack);

        navigation.CurrentViewModel = new SettingsViewModel();
        Assert.True(navigation.CanGoBack);

        Assert.True(await navigation.GoBackAsync());
        Assert.IsType<HomeViewModel>(navigation.CurrentViewModel);
        Assert.False(navigation.CanGoBack);

        Assert.Contains(nameof(RecordingNavigationService.CanGoBack), changed);
    }

    [Fact]
    public async Task TheStorageService_RoundTripsJsonWithoutTouchingTheDisk()
    {
        InMemoryStorageService storage = new();
        string path = storage.GetDataFilePath("settings.json");

        Assert.False(storage.FileExists(path));

        await storage.WriteJsonAsync(path, new Settings { Culture = "es-ES" });

        Assert.True(storage.FileExists(path));
        Assert.Equal("es-ES", (await storage.ReadJsonAsync<Settings>(path))?.Culture);
        Assert.Single(storage.Files);
    }

    [Fact]
    public void TheLocalizationService_ReturnsTheKeyWhenNothingResolves()
    {
        DictionaryLocalizationService localization =
            DictionaryLocalizationService.For("en-US", new Dictionary<string, string> { ["Home_Title"] = "Orders" });

        Assert.Equal("Orders", localization.GetString("Home_Title"));
        Assert.Equal("Missing_Key", localization.GetString("Missing_Key"));
    }

    [Fact]
    public void TheLocalizationService_RaisesCultureChangedAndRefusesWhatItDoesNotHave()
    {
        DictionaryLocalizationService localization = new(
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en-US"] = new Dictionary<string, string> { ["Home_Title"] = "Orders" },
                ["es-ES"] = new Dictionary<string, string> { ["Home_Title"] = "Pedidos" }
            });

        int raised = 0;
        localization.CultureChanged += (_, _) => raised++;

        localization.SetCulture("es-ES");

        Assert.Equal("Pedidos", localization.GetString("Home_Title"));
        Assert.Equal(1, raised);

        // Setting the culture it is already on is not a change.
        localization.SetCulture("es-ES");
        Assert.Equal(1, raised);

        Assert.Throws<NotSupportedException>(() => localization.SetCulture("fr-FR"));
    }

    /// <summary>
    /// A command built with the inline dispatcher has finished by the time Execute returns, which
    /// is what makes a test readable without pumping anything.
    /// </summary>
    [Fact]
    public void TheInlineDispatcher_RunsWorkWhereItWasCalled()
    {
        InlineUIDispatcher dispatcher = new();
        RelayCommand command = new RelayCommandFactory(dispatcher).Create(() => { });

        command.Execute(null);

        Assert.True(dispatcher.CheckAccess());
    }

    [Fact]
    public void TheInlineDispatcher_HoldsPostedWorkBackWhenAskedTo()
    {
        InlineUIDispatcher dispatcher = new() { AutoFlushPosts = false };
        int ran = 0;

        dispatcher.Post(() => ran++);
        dispatcher.Post(() => ran++);

        Assert.Equal(0, ran);
        Assert.Equal(2, dispatcher.PostCount);

        dispatcher.FlushPosts();

        Assert.Equal(2, ran);
        Assert.Empty(dispatcher.PendingPosts);
    }

    [Fact]
    public void ThePumpingDispatcher_RunsPostedWorkOnlyWhenDrained()
    {
        PumpingUIDispatcher dispatcher = new();
        int ran = 0;

        dispatcher.Post(() => ran++);

        Assert.Equal(0, ran);

        dispatcher.Drain();

        Assert.Equal(1, ran);
    }

    private sealed class Settings
    {
        public string Culture { get; set; } = "en-US";
    }
}
