using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Research.Logic
{
    // 研究ボタンの活性とツールチップ。活性の正本はサーバーの state だけ
    // The research button's availability and tooltip; only the server state decides availability
    public sealed class ResearchButtonState
    {
        public bool Completed { get; }
        public bool Interactable { get; }
        public LocalizationKey TooltipKey { get; }

        public ResearchButtonState(bool completed, bool interactable, LocalizationKey tooltipKey)
        {
            Completed = completed;
            Interactable = interactable;
            TooltipKey = tooltipKey;
        }
    }

    // ノードカードの 4 状態（完了・研究可能・前提未達・通常）の導出結果
    // The derived four card states (completed, ready, locked, plain)
    public readonly struct NodeCardState
    {
        public bool Completed { get; }
        public bool Ready { get; }
        public bool Locked { get; }

        public NodeCardState(bool completed, bool ready, bool locked)
        {
            Completed = completed;
            Ready = ready;
            Locked = locked;
        }
    }
}
