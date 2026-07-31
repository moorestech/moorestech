using Mooresmaster.Model.ChallengesModule;

namespace Client.Game.InGame.Tutorial
{
    public interface ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial);

    }
}