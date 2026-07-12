using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly PlaceSystemSelector _placeSystemSelector;

        private IPlaceSystem _currentPlaceSystem;
        private IPlacementTarget _lastTarget;

        // 「今何を設置しようとしているか」の唯一の所有者。書き込みはSetTargetのみ
        // The single owner of "what is being placed now"; writes go through SetTarget only
        public IPlacementTarget CurrentTarget { get; private set; }

        public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector)
        {
            _placeSystemSelector = placeSystemSelector;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
        }

        public void SetTarget(IPlacementTarget target)
        {
            CurrentTarget = target;
        }

        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;

            // 選択の寿命はPlaceBlock滞在中のみ。離脱時にターゲットも破棄する
            // Selection lives only while in PlaceBlock; drop the target on leave
            CurrentTarget = null;
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
