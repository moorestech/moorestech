using Client.Game.Story;
using Client.Game.UI.Inventory;
using Client.Story;
using Cysharp.Threading.Tasks;
using MainGame.UnityView.Control;

namespace Client.Game.UI.UIState
{
    public class StoryState : IUIState
    {
        private readonly PlayerSkitStarterDetector _playerSkitStarterDetector;
        private readonly SkitManager _skitManager;
        private readonly HotBarView _hotBarView;

        private UIStateEnum _currentNext;

        public StoryState(PlayerSkitStarterDetector playerSkitStarterDetector, SkitManager skitManager, HotBarView hotBarView)
        {
            _playerSkitStarterDetector = playerSkitStarterDetector;
            _skitManager = skitManager;
            _hotBarView = hotBarView;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            PlayStory().Forget();
        }

        private async UniTask PlayStory()
        {
            _hotBarView.SetActive(false);
            InputManager.MouseCursorVisible(true);
            _currentNext = UIStateEnum.Current;

            var csv = _playerSkitStarterDetector.CurrentSkitStarterObject.ScenarioCsv;
            await _skitManager.StartStory(csv);

            _hotBarView.SetActive(true);
            InputManager.MouseCursorVisible(false);
            _currentNext = UIStateEnum.GameScreen;
        }

        public UIStateEnum GetNext()
        {
            return _currentNext;
        }

        public void OnExit()
        {
        }
    }
}