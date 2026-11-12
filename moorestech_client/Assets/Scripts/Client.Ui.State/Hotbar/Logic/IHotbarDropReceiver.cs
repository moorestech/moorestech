namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ホットバーの落とし先が源を受け取る口。受け手は自分の落とし先を添えて IHotbarCommands.Drop へ渡すだけで、結果は判断しない
    /// Port through which a hotbar drop target receives a source; the receiver only passes it with its own target to IHotbarCommands.Drop and never decides the outcome
    /// </summary>
    public interface IHotbarDropReceiver
    {
        void Receive(HotbarDragSource source);
    }
}
