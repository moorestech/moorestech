using MainGame.UnityView.UI.Mission;

namespace MainGame.Presenter.Mission.Missions
{
    public class GetItemMission : MissionBase 
    {
        public GetItemMission(int priority, string missionNameKey) : base(priority, missionNameKey)
        {
        }

        protected override void IfNotDoneUpdate()
        {
            TODO
        }
    }
}