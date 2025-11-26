public interface IUIDispatcher
{
    bool CheckAccess();
    void Post(Action action);
    void Invoke(Action action);
}