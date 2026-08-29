using Mooresmaster.Model.ChallengesModule;

namespace Client.Game.InGame.Tutorial
{
    public interface ITutorialViewManager
    {
        // どのtutorialTypeを担うかを自身が名乗る。振り分け先を外から手配線する必要をなくす
        // Each manager names the tutorialType it serves, so nothing has to wire the dispatch from outside
        string TutorialType { get; }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial);
    }
}
