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

        // 「今何を設置しようとしているか」の唯一の所有者。書き込みはSetTargetのみ
        // The single owner of "what is being placed now"; writes go through SetTarget only
        public IPlacementTarget CurrentTarget { get; private set; }

        // 設置対象と同じ書き込み点でしか変わらない由来。両者は構造的に乖離しない
        // The origin changes only at the same write point as the target, so the two cannot drift apart
        public PlacementOrigin CurrentOrigin { get; private set; }
        public IObservable<IPlacementTarget> OnTargetChanged => _onTargetChanged;

        public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector)
        {
            _placeSystemSelector = placeSystemSelector;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
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

        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;

            // 選択の寿命はPlaceBlock滞在中のみ。離脱時にターゲットと由来を同時に破棄する
            // Selection lives only while in PlaceBlock; drop the target and its origin together on leave
            CurrentTarget = null;
            CurrentOrigin = PlacementOrigin.None;
            _onTargetChanged.OnNext(null);
            _lastTarget = null;
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
        }
    }
}
