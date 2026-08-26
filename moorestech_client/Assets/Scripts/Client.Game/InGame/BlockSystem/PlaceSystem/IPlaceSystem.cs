using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IPlaceSystem
    {
        // 今このフレームでホイール入力を消費しているか。消費側は設置対象の種別から再導出しない
        // Whether this system is consuming wheel input this frame; consumers must not re-derive it from the target kind
        public bool OwnsWheelInput { get; }

        public void Enable();

        public void ManualUpdate(PlaceSystemUpdateContext context);

        public void Disable();
    }

    public readonly struct PlaceSystemUpdateContext
    {
        // 設置対象（null = 未選択）。具体型を知るのはSelectorと各システムのみ
        // The placement target (null = nothing selected); only the selector and each system know concrete types
        public readonly IPlacementTarget Target;
        public readonly bool IsSelectionChanged;

        // このフレームの不可理由/案内の書き込み先
        // Sink for this frame's block reasons/notices
        public readonly PlacementFeedback Feedback;

        public PlaceSystemUpdateContext(IPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            Target = target;
            IsSelectionChanged = isSelectionChanged;
            Feedback = feedback;
        }
    }
}
