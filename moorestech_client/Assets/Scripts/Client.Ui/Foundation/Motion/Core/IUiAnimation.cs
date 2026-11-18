namespace Client.Ui.Foundation.Motion.Core
{
    /// <summary>
    /// 開始からの経過時間で状態を書く動き。tween と慣性が同じ走行器で動くための口
    /// A motion that writes its state from the elapsed time since start; the shared entry that lets tween and inertia run on one runner
    /// </summary>
    public interface IUiAnimation
    {
        // 頭からの状態に戻す
        // Return to the state at the head
        void Begin();

        // 開始からの経過で状態を書き、終わったら true
        // Write the state for the elapsed time since start; true once finished
        bool Advance(float elapsedMilliseconds);
    }
}
