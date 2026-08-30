using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押しアクションを実行したか、実行してUI遷移まで起きたかを区別して返す
    ///     Distinguishes "no action ran" from "an action ran" and from "an action ran and asks for a UI transition"
    /// </summary>
    public readonly struct InteractExecuteResult
    {
        // 実行しなかったフレームだけ後続の入力判定へ進ませる
        // Only a frame that ran nothing falls through to the later input checks
        public static readonly InteractExecuteResult NotHandled = new(false, null);

        public bool IsHandled { get; }
        public UITransitContext TransitContext { get; }

        private InteractExecuteResult(bool isHandled, UITransitContext transitContext)
        {
            IsHandled = isHandled;
            TransitContext = transitContext;
        }

        // UI遷移を伴わない実行
        // An execution that changes no UI state
        public static InteractExecuteResult Handled()
        {
            return new InteractExecuteResult(true, null);
        }

        public static InteractExecuteResult Transit(UITransitContext transitContext)
        {
            return new InteractExecuteResult(true, transitContext);
        }
    }
}
