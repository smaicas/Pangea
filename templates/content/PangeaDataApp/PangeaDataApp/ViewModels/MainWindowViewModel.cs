using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PangeaDataApp.Data;
using PangeaDataApp.Domain;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace PangeaDataApp.ViewModels;

/// <summary>
/// The list, the form that adds to it, and what the application can tell the user about its own
/// database.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here creates a <see cref="AppDbContext"/> and keeps it.
/// <see cref="IPangeaDbContext{TContext}"/> builds one per operation from the pooled factory and
/// disposes it before the call returns - a context held by a view model for the life of the window
/// would track every row it ever loaded and serve values that changed an hour ago.
/// </para>
/// <para>
/// The schema is already in place by the time this runs: the data feature registers a startup
/// initializer, and the main window is not shown until it has finished.
/// </para>
/// </remarks>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPangeaDbContext<AppDbContext> _db;
    private readonly IDatabaseMaintenance<AppDbContext> _maintenance;

    [Binding] private ObservableCollection<Note> _notes = [];

    [Binding]
    [Required(ErrorMessage = "A title is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Between 2 and 80 characters.")]
    private string _newTitle = "";

    [Binding] private string _newBody = "";
    [Binding] private Note? _selectedNote;
    [Binding] private string _status = "";
    [Binding] private string _databaseSummary = "";

    public MainWindowViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _db = serviceProvider.GetRequiredService<IPangeaDbContext<AppDbContext>>();
        _maintenance = serviceProvider.GetRequiredService<IDatabaseMaintenance<AppDbContext>>();

        // Started rather than awaited: a constructor cannot wait, and the window is better up with
        // an empty list for a moment than not up at all. Failures land in Status.
        _ = RefreshAsync();
    }

    public bool HasNotes => Notes.Count > 0;

    /// <summary>
    /// Reads NewTitle, so the generator raises CanExecuteChanged from its setter, and IsBusy, which
    /// ViewModelBase raises for every command when the first one starts and the last one ends.
    /// </summary>
    public bool CanAddNote => !string.IsNullOrWhiteSpace(NewTitle) && !HasErrors && !IsBusy;

    public bool CanDeleteNote => SelectedNote is not null && !IsBusy;

    public RelayCommand AddNoteCommand => CreateCommand(AddNoteAsync, () => CanAddNote);

    public RelayCommand DeleteNoteCommand => CreateCommand(DeleteNoteAsync, () => CanDeleteNote);

    public RelayCommand RefreshCommand => CreateCommand(RefreshAsync, () => !IsBusy);

    public RelayCommand BackupCommand => CreateCommand(BackupAsync, () => !IsBusy);

    public RelayCommand CompactCommand => CreateCommand(CompactAsync, () => !IsBusy);

    private async Task AddNoteAsync()
    {
        if (!ValidateAll()) return;

        // The tidying is a rule, not wiring, so it lives in Domain where it can be asked directly.
        // What is left here is what needs an application around it to mean anything.
        if (NoteDraft.From(NewTitle, NewBody) is not { } draft) return;

        await RunAsync(() => $"Saved '{draft.Title}'.", async () =>
        {
            // Runs the change and saves it, one write at a time: SQLite has a single writer, and
            // the feature queues writes rather than letting the second one fail.
            await _db.WriteAsync((context, token) =>
            {
                context.Notes.Add(new Note { Title = draft.Title, Body = draft.Body });
                return Task.CompletedTask;
            });

            NewTitle = "";
            NewBody = "";

            await LoadAsync();
        });
    }

    private async Task DeleteNoteAsync()
    {
        if (SelectedNote is not { } note) return;

        await RunAsync(() => $"Deleted '{note.Title}'.", async () =>
        {
            await _db.WriteAsync(
                (context, token) => context.Notes.Where(row => row.Id == note.Id).ExecuteDeleteAsync(token));

            await LoadAsync();
        });
    }

    private async Task BackupAsync()
    {
        string path = string.Empty;

        await RunAsync(() => $"Backed up to {path}", async () =>
        {
            path = await _maintenance.BackupAsync();
            await LoadAsync();
        });
    }

    private async Task CompactAsync() =>
        await RunAsync(() => "Compacted.", async () =>
        {
            await _maintenance.CompactAsync();
            await LoadAsync();
        });

    private Task RefreshAsync() => RunAsync(() => null, LoadAsync);

    /// <summary>
    /// Reloads the list and the database summary underneath it. Called by everything that changed
    /// either, without the busy handling - that belongs to the command that started the work.
    /// </summary>
    private async Task LoadAsync()
    {
        // Comes back as a collection built on the UI thread, ready to bind to.
        Notes = await _db.ToObservableAsync(context => context.Notes.OrderByDescending(note => note.CreatedUtc));
        OnPropertyChanged(nameof(HasNotes));

        DatabaseInfo info = await _maintenance.GetInfoAsync();

        DatabaseSummary =
            $"{info.ProviderName} · {FileSize.Describe(info.SizeBytes)} · " +
            $"{info.AppliedMigrations.Count} migration(s) applied · " +
            $"{_maintenance.GetBackups().Count} backup(s)" +
            (info.FilePath is null ? "" : Environment.NewLine + info.FilePath);
    }

    /// <summary>
    /// The busy flag, the refresh and the failure handling every command needs, in one place.
    /// </summary>
    /// <remarks>
    /// The status is a function rather than a string because some of it is only known once the work
    /// has run - the path a backup was written to, for instance.
    /// </remarks>
    private async Task RunAsync(Func<string?> status, Func<Task> work)
    {
        // No busy flag of its own: ViewModelBase.IsBusy is true while any command it created is
        // running, and a command already refuses to run twice at once.
        try
        {
            await work();

            if (status() is { } message) Status = message;
        }
        catch (Exception ex)
        {
            // Shown rather than thrown: a failed query is something the user can retry, and the
            // window is more useful than a crash dialog.
            Status = ex.Message;
        }
    }

}
