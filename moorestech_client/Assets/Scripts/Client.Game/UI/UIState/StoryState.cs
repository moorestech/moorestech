using Client.Game.Story;
using Client.Game.UI.Inventory;
using Client.Story;
using Cysharp.Threading.Tasks;
using MainGame.UnityView.Control;

namespace Client.Game.UI.UIState
{
    public class StoryState : IUIState
    {
        private readonly PlayerStoryStarter _playerStoryStarter;
        private readonly StoryManager _storyManager;
        private readonly HotBarView _hotBarView;

        private UIStateEnum _currentNext;

        public StoryState(PlayerStoryStarter playerStoryStarter, StoryManager storyManager, HotBarView hotBarView)
        {
            _playerStoryStarter = playerStoryStarter;
            _storyManager = storyManager;
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

            var csv = _playerStoryStarter.CurrentStoryStarterObject.ScenarioCsv;
            await _storyManager.StartStory(csv);

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