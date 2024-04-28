using Client.Game.Common;
using Client.Game.InGame.UI.Inventory;
using Client.Game.Skit;
using Client.Game.Skit.Starter;
using Client.Input;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.UIState
{
    public class StoryState : IUIState
    {
        private readonly PlayerSkitStarterDetector _playerSkitStarterDetector;
        private readonly SkitManager _skitManager;

        private UIStateEnum _currentNext;

        public StoryState(PlayerSkitStarterDetector playerSkitStarterDetector, SkitManager skitManager, HotBarView hotBarView)
        {
            _playerSkitStarterDetector = playerSkitStarterDetector;
            _skitManager = skitManager;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            PlayStory().Forget();
        }

        public UIStateEnum GetNext()
        {
            return _currentNext;
        }

        public void OnExit()
        {
        }

        private async UniTask PlayStory()
        {
            _currentNext = UIStateEnum.Current;

            GameStateController.Instance.ChangeState(GameStateType.Skit);

            var csv = _playerSkitStarterDetector.CurrentSkitStarterObject.ScenarioCsv;
            await _skitManager.StartStory(csv);

            GameStateController.Instance.ChangeState(GameStateType.InGame);

            _currentNext = UIStateEnum.GameScreen;
        }
    }
}