using Client.Game.InGame.Tutorial;
using Mooresmaster.Model.ChallengesModule;

namespace Client.DebugSystem.Skit
{
    public class MapObjectTest : IMapObjectPin
    {
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            return this;
        }
        public void CompleteTutorial()
        {
        }
        public void SetActive(bool active)
        {
        }
    }
}