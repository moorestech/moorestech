using Client.Game.InGame.Tutorial;
using Mooresmaster.Model.ChallengesModule;

namespace Client.DebugSystem.Skit
{
    public class MapObjectTest : ITutorialWorldPin
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.mapObjectPin;

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
        public void BeginSkitSuppress()
        {
        }
        public void EndSkitSuppress()
        {
        }
    }
}
