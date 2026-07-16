using Client.Input;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Control.ViewMode
{
    public class PlayerViewModeController : IStartable, ITickable
    {
        private readonly IPlayerViewApplier _applier;
        private PlayerViewMode _currentMode = PlayerViewMode.ThirdPerson;

        public PlayerViewModeController(IPlayerViewApplier applier)
        {
            _applier = applier;
        }

        public void Start()
        {
            // Scene上の表示SingletonがAwakeした後に初期視点を同期する
            // Synchronize the initial view after scene presentation singletons have awakened
            _applier.SetViewMode(_currentMode);
        }

        public void Tick()
        {
            // UI表示状態に関係なくV入力を受け付ける
            // Accept the V input regardless of the visible UI state
            if (HybridInput.GetKeyDown(KeyCode.V)) ToggleViewMode();
        }

        public void ToggleViewMode()
        {
            _currentMode = _currentMode == PlayerViewMode.ThirdPerson
                ? PlayerViewMode.FirstPerson
                : PlayerViewMode.ThirdPerson;
            _applier.SetViewMode(_currentMode);
        }

        public PlayerViewMode GetCurrentMode()
        {
            return _currentMode;
        }
    }
}
