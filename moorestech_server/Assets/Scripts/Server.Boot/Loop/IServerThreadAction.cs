namespace Server.Boot.Loop
{
    // tick末尾で走らせる処理。停止通知の渡し忘れを型で消す
    // Work run at the tick end; the type removes any chance of forgetting the stop notification
    public interface IServerThreadAction
    {
        void Run();
        void OnServerStopped();
    }
}
