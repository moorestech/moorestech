namespace Client.Starter.EventMode
{
    internal interface IEventModeAutoStartActions
    {
        void StartFreshWorld();
        void QuitBecauseTitleGatesTimedOut(float deadlineSeconds);
    }
}
