using Avalonia.Controls;
using PangeaSupabaseApp.ViewModels;

namespace PangeaSupabaseApp.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    /// <summary>
    /// Holds the pull gesture's spinner until the server has actually answered.
    /// </summary>
    /// <remarks>
    /// The deferral is the whole point. Without it the visualiser snaps back the instant the handler
    /// returns - which, for an async refresh, is immediately - and the gesture reads as having done
    /// nothing at all.
    /// </remarks>
    private async void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e)
    {
        if (DataContext is not HomeViewModel screen) return;

        RefreshCompletionDeferral deferral = e.GetDeferral();

        try
        {
            await screen.RefreshAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
