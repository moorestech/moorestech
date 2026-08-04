using Client.Game.InGame.Tutorial;
using Mooresmaster.Model.ChallengesModule;

namespace Client.DebugSystem.Skit
{
    public class MapObjectTest : IMapObjectPin, IVeinPin
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            return this;
        }
        public void CompleteTutorial()
        {
        }
        public void SetActive(bool active)
        {
        }
        public bool IsActiveSelf()
        {
            return false;
        }
    }
}
