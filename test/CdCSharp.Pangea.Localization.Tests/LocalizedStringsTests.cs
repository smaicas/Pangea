using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Localization.Tests.Infrastructure;
using System.ComponentModel;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// What makes a change of language visible: one object every binding goes through, which announces
/// that every string it holds has changed.
/// </summary>
public class LocalizedStringsTests
{
    private static Dictionary<string, Dictionary<string, string>> TwoLanguages() => new()
    {
        ["en-US"] = new Dictionary<string, string> { ["Home_Title"] = "Orders" },
        ["es-ES"] = new Dictionary<string, string> { ["Home_Title"] = "Pedidos" }
    };

    [Fact]
    public void AKeyIsReadInWhateverCultureIsCurrent()
    {
        StubLocalizationService localization = new(TwoLanguages());
        using LocalizedStrings strings = new(localization);

        Assert.Equal("Orders", strings["Home_Title"]);

        localization.SetCulture("es-ES");

        Assert.Equal("Pedidos", strings["Home_Title"]);
    }

    /// <summary>
    /// "Item[]" is what a binding listens for. Any other name and the window keeps the old text.
    /// </summary>
    [Fact]
    public void ChangingTheCulture_AnnouncesThatEveryStringChanged()
    {
        StubLocalizationService localization = new(TwoLanguages());
        using LocalizedStrings strings = new(localization);

        List<string?> announced = [];
        ((INotifyPropertyChanged)strings).PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        localization.SetCulture("es-ES");

        Assert.Equal(["Item[]"], announced);
    }

    [Fact]
    public void SettingTheCultureItIsAlreadyOn_AnnouncesNothing()
    {
        StubLocalizationService localization = new(TwoLanguages());
        using LocalizedStrings strings = new(localization);

        int announced = 0;
        ((INotifyPropertyChanged)strings).PropertyChanged += (_, _) => announced++;

        localization.SetCulture("en-US");

        Assert.Equal(0, announced);
    }

    /// <summary>
    /// The culture can be set from a background thread - a settings file being read at startup -
    /// and a binding updated off the UI thread is an exception nobody sees coming.
    /// </summary>
    [Fact]
    public void WhenTheChangeArrivesOffTheUIThread_TheAnnouncementIsMarshalled()
    {
        StubLocalizationService localization = new(TwoLanguages());
        RecordingDispatcher dispatcher = new() { IsOnUIThread = false };
        using LocalizedStrings strings = new(localization, dispatcher);

        int announced = 0;
        ((INotifyPropertyChanged)strings).PropertyChanged += (_, _) => announced++;

        localization.SetCulture("es-ES");

        Assert.Equal(1, dispatcher.PostCount);
        Assert.Equal(1, announced);
    }

    [Fact]
    public void OnTheUIThread_TheAnnouncementIsNotQueued()
    {
        StubLocalizationService localization = new(TwoLanguages());
        RecordingDispatcher dispatcher = new() { IsOnUIThread = true };
        using LocalizedStrings strings = new(localization, dispatcher);

        localization.SetCulture("es-ES");

        Assert.Equal(0, dispatcher.PostCount);
    }

    [Fact]
    public void DisposingStopsListening()
    {
        StubLocalizationService localization = new(TwoLanguages());
        LocalizedStrings strings = new(localization);

        int announced = 0;
        ((INotifyPropertyChanged)strings).PropertyChanged += (_, _) => announced++;

        strings.Dispose();
        localization.SetCulture("es-ES");

        Assert.Equal(0, announced);
    }

    private sealed class RecordingDispatcher : IUIDispatcher
    {
        public bool IsOnUIThread { get; set; } = true;

        public int PostCount { get; private set; }

        public bool CheckAccess() => IsOnUIThread;

        public void Post(Action action)
        {
            PostCount++;
            action();
        }

        public void Invoke(Action action) => action();

        public T Invoke<T>(Func<T> callback) => callback();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }
}
