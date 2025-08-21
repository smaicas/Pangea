namespace CdCSharp.Pangea.Core.Abstractions;

public interface IPangeaApplicationContext
{
    void AddStyle(object style);
    void RemoveStyle(object style);
    bool HasStyle<T>() where T : class;
    T? GetRequiredService<T>() where T : class;
    object? GetApplication();
}