namespace CdCSharp.Pangea.Core.Abstractions;

public interface INavigationAware
{
    Task OnNavigatedToAsync(NavigationParameter? parameter);
    Task OnNavigatedFromAsync();
    Task<bool> CanNavigateAwayAsync();
}

public class NavigationParameter
{
    public object? Data { get; set; }
    public TaskCompletionSource<object?>? ResultSource { get; internal set; }
    public Dictionary<string, object> Properties { get; } = new();

    public T? GetData<T>() => Data is T data ? data : default;

    public void SetProperty(string key, object value) => Properties[key] = value;

    public T? GetProperty<T>(string key) =>
        Properties.TryGetValue(key, out object? value) && value is T typed ? typed : default;
}