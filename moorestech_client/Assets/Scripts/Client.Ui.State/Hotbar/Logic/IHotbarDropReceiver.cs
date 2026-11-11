namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ホットバーの落とし先が源を受け取る口。割当の要求まで受け手が済ませ、断る理由のログも受け手が出す
    /// Port through which a hotbar drop target receives a source; the receiver sends the assignment request and logs any refusal
    /// </summary>
    public interface IHotbarDropReceiver
    {
        void Receive(HotbarDragSource source);
    }
}
