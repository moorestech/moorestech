using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Mission;

namespace MainGame.Presenter.Mission.Missions
{
    public class InputKeyMission : MissionBase
    {
        private readonly InputKey _inputKey;

        public InputKeyMission(int priority, string missionNameKey, InputKey inputKey) : base(priority, missionNameKey)
        {
            _inputKey = inputKey;
        }

        protected override void IfNotDoneUpdate()
        {
            if (_inputKey.GetKeyDown) Done();
        }
    }
}