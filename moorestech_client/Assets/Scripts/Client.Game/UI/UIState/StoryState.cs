using Client.Game.Story;
using Client.Story;
using Cysharp.Threading.Tasks;

namespace Client.Game.UI.UIState
{
    public class StoryState : IUIState
    {
        private readonly PlayerStoryStarter _playerStoryStarter;
        private readonly StoryManager _storyManager;

        private UIStateEnum _currentNext;
        
        public StoryState(PlayerStoryStarter playerStoryStarter, StoryManager storyManager)
        {
            _playerStoryStarter = playerStoryStarter;
            _storyManager = storyManager;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            PlayStory().Forget();
        }
        
        private async UniTask PlayStory()
        {
            _currentNext = UIStateEnum.Current;
            var csv = _playerStoryStarter.CurrentStoryStarterObject.ScenarioCsv;
            await _storyManager.StartStory(csv);
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