using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly IPlaceSystemSelector _placeSystemSelector;
        private readonly IPlacementFeedbackPresenter _feedbackPresenter;
        private readonly PlacementFeedback _feedback = new();

        private IPlaceSystem _currentPlaceSystem;
        private IPlacementTarget _lastTarget;
        private readonly Subject<IPlacementTarget> _onTargetChanged = new();
        private readonly ReactiveProperty<bool> _isWheelOwnedByTool = new(false);

        // 「今何を設置しようとしているか」の唯一の所有者。書き込みはSetTargetのみ
        // The single owner of "what is being placed now"; writes go through SetTarget only
        public IPlacementTarget CurrentTarget { get; private set; }

        // 設置対象と同じ書き込み点でしか変わらない由来。両者は構造的に乖離しない
        // The origin changes only at the same write point as the target, so the two cannot drift apart
        public PlacementOrigin CurrentOrigin { get; private set; }
        public IObservable<IPlacementTarget> OnTargetChanged => _onTargetChanged;

        // ホイールを実際に消費している設置系がここへプッシュする。消費側は種別から再導出しない
        // The place system actually consuming the wheel pushes here; consumers must not re-derive it from the target kind
        public bool IsWheelOwnedByTool => _isWheelOwnedByTool.Value;
        public IObservable<bool> OnWheelOwnedByToolChanged => _isWheelOwnedByTool;

        // 表示面へ触るのは初期化ではなくManualUpdate/Disableの仕事。ctorはフィールドを埋めるだけにする
        // Touching the view is ManualUpdate/Disable's job, not construction; the ctor only fills fields
        public PlaceSystemStateController(IPlaceSystemSelector placeSystemSelector, IPlacementFeedbackPresenter feedbackPresenter)
        {
            _placeSystemSelector = placeSystemSelector;
            _feedbackPresenter = feedbackPresenter;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            CurrentOrigin = PlacementOrigin.NonHotbar;
        }

        // 対象同一でも由来変化で通知する
        // Notifies even for an identical target when only the origin differs
        public void SetTarget(IPlacementTarget target, PlacementOrigin origin)
        {
            if (Equals(CurrentTarget, target) && CurrentOrigin.Equals(origin)) return;
            CurrentTarget = target;
            CurrentOrigin = origin;
            _onTargetChanged.OnNext(target);
        }

        public void SetWheelOwnedByTool(bool isOwned)
        {
            _isWheelOwnedByTool.Value = isOwned;
        }

        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;

            // 設置モード離脱時は理由表示も消す
            // Leaving placement mode also clears the reason tooltip
            _feedback.Clear();
            _feedbackPresenter.Hide();

            // 選択の寿命はPlaceBlock滞在中のみ。離脱時にターゲットと由来を同時に破棄する
            // Selection lives only while in PlaceBlock; drop the target and its origin together on leave
            CurrentTarget = null;
            CurrentOrigin = PlacementOrigin.NonHotbar;
            _onTargetChanged.OnNext(null);
            _lastTarget = null;
            _isWheelOwnedByTool.Value = false;
        }

        public void ManualUpdate()
        {
            var isSelectionChanged = !Equals(_lastTarget, CurrentTarget);
            _lastTarget = CurrentTarget;

            // 理由はフレームごとに集め直す
            // Reasons are re-collected every frame
            _feedback.Clear();
            var updateContext = new PlaceSystemUpdateContext(CurrentTarget, isSelectionChanged, _feedback);
            var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

            if (_currentPlaceSystem != nextPlaceSystem)
            {
                _currentPlaceSystem.Disable();
                _currentPlaceSystem = nextPlaceSystem;
                _currentPlaceSystem.Enable();
            }

            _currentPlaceSystem.ManualUpdate(updateContext);

            // 消費の有無はドラッグ中など毎フレーム変わりうるため、更新後の実状態をここで取り込む
            // Whether the wheel is consumed can change per frame (e.g. mid-drag), so pull the post-update truth here
            SetWheelOwnedByTool(_currentPlaceSystem.OwnsWheelInput);

            // 更新後の理由・案内をツールチップへ反映
            // Pushes the collected reasons/notices to the tooltip
            _feedbackPresenter.Present(_feedback);
        }
    }
}
