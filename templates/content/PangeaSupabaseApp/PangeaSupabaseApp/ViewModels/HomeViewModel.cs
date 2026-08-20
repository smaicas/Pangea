using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using PangeaSupabaseApp.Data;
using PangeaSupabaseApp.Services;
using System.Collections.ObjectModel;

namespace PangeaSupabaseApp.ViewModels;

/// <summary>
/// The notes, and the box that adds one.
/// </summary>
/// <remarks>
/// Shows the shape worth copying: the list is drawn from the cache before anything is asked of the
/// server, a write appears immediately whether or not it lands, and what could not be sent is
/// counted rather than lost.
/// </remarks>
public partial class HomeViewModel : ViewModelBase
{
    private readonly NotesRepository _repository;
    private readonly IDialogService _dialogs;
    private readonly IUIDispatcher _dispatcher;

    [Binding] private string _newTitle = "";
    [Binding] private int _pending;

    public HomeViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _repository = serviceProvider.GetRequiredService<NotesRepository>();
        _dialogs = serviceProvider.GetRequiredService<IDialogService>();
        _dispatcher = serviceProvider.GetRequiredService<IUIDispatcher>();

        Notes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));

        // Through Subscribe, so the repository lets go of this screen when navigation drops
        // it. Subscribing directly is the leak nothing makes visible until the application
        // has been used for a while.
        Subscribe<IReadOnlyList<Note>>(
            handler => _repository.Changed += handler,
            handler => _repository.Changed -= handler,
            OnNotesChanged);

        _ = LoadAsync();
    }

    public ObservableCollection<Note> Notes { get; } = [];

    public bool IsEmpty => Notes.Count == 0;

    public bool HasPending => Pending > 0;

    /// <summary>Reads NewTitle, so the generator raises CanExecuteChanged from its setter.</summary>
    public bool CanAdd => !IsBusy && !string.IsNullOrWhiteSpace(NewTitle);

    public RelayCommand AddCommand => CreateCommand(AddAsync, () => CanAdd);

    public RelayCommand<Note> DeleteCommand => CreateCommand<Note>(DeleteAsync, note => note is not null);

    /// <summary>Asks the server again, and completes when it has answered.</summary>
    public Task RefreshAsync() => _repository.RefreshAsync();

    private async Task LoadAsync()
    {
        IReadOnlyList<Note> known = await _repository.AllAsync();

        await _dispatcher.InvokeAsync(() => Replace(known));
    }

    /// <summary>
    /// The repository raises this from wherever the work finished, which is not the UI thread.
    /// </summary>
    private void OnNotesChanged(object? sender, IReadOnlyList<Note> notes) => _dispatcher.Post(() => Replace(notes));

    private void Replace(IReadOnlyList<Note> notes)
    {
        Notes.Clear();

        foreach (Note note in notes) Notes.Add(note);

        _ = CountPendingAsync();
    }

    private async Task CountPendingAsync()
    {
        int pending = await _repository.PendingAsync();

        await _dispatcher.InvokeAsync(() =>
        {
            Pending = pending;
            OnPropertyChanged(nameof(HasPending));
        });
    }

    private async Task AddAsync()
    {
        // ViewModelBase.IsBusy is true while this runs, and false when it ends however it ends.
        await _repository.AddAsync(NewTitle);

        NewTitle = "";
    }

    private async Task DeleteAsync(Note? note)
    {
        if (note is null) return;

        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete note", $"\u201c{note.Title}\u201d will be removed.", "Delete", "Keep");

        if (!confirmed) return;

        await _repository.DeleteAsync(note.Id);
    }
}
