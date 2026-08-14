using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly PlaceSystemSelector _placeSystemSelector;

        private IPlaceSystem _currentPlaceSystem;
        private IPlacementTarget _lastTarget;
        private readonly Subject<IPlacementTarget> _onTargetChanged = new();
        private readonly ReactiveProperty<bool> _isWheelOwnedByTool = new(false);

        // 「今何を設置しようとしているか」の唯一の所有者。書き込みはSetTargetのみ
        // The single owner of "what is being placed now"; writes go through SetTarget only
        public IPlacementTarget CurrentTarget { get; private set; }
        public IObservable<IPlacementTarget> OnTargetChanged => _onTargetChanged;

        // ホイールを実際に消費している設置系がここへプッシュする。消費側は種別から再導出しない
        // The place system actually consuming the wheel pushes here; consumers must not re-derive it from the target kind
        public bool IsWheelOwnedByTool => _isWheelOwnedByTool.Value;
        public IObservable<bool> OnWheelOwnedByToolChanged => _isWheelOwnedByTool;

        public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector)
        {
            _placeSystemSelector = placeSystemSelector;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
        }

        public void SetTarget(IPlacementTarget target)
        {
            if (Equals(CurrentTarget, target)) return;
            CurrentTarget = target;
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

            // 選択の寿命はPlaceBlock滞在中のみ。離脱時にターゲットも破棄する
            // Selection lives only while in PlaceBlock; drop the target on leave
            CurrentTarget = null;
            _onTargetChanged.OnNext(null);
            _lastTarget = null;
            _isWheelOwnedByTool.Value = false;
        }

        public void ManualUpdate()
        {
            var isSelectionChanged = !Equals(_lastTarget, CurrentTarget);
            _lastTarget = CurrentTarget;

            var updateContext = new PlaceSystemUpdateContext(CurrentTarget, isSelectionChanged);
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
        }
    }
}
