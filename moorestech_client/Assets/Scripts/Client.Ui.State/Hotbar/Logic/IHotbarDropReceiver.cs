namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// 落とし先が源を受ける口。結果は判断しない
    /// Drop target receives a source; never decides outcome
    /// </summary>
    public interface IHotbarDropReceiver
    {
        void Receive(HotbarDragSource source);
    }
}
