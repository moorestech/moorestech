using Client.Ui.State.EventMode;

namespace Client.Starter.EventMode
{
    /// <summary>
    /// 開始ゲートから見た DI 前の言語ゲートの画面。開く順序（開く → 選択 → 武装）をテストから観測する境界（前例 IEventIdleWatchArmer と同じく、使う側の Starter が持つ）
    /// The pre-DI language-gate screen as the start gate sees it; the seam that lets tests observe the order (open, choose, arm) (like IEventIdleWatchArmer, owned by the using side, the Starter)
    /// </summary>
    public interface IEventLanguageGateScreen
    {
        void Open(EventLanguageGate gate);
    }
}
