using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    // uiState判定はUITK側（Client.Ui）。値そのまま公開
    // uiState matching is on the UITK side (Client.Ui); this just publishes values
    public class KeyControlTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.keyControl;

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (KeyControlTutorialParam)tutorial.TutorialParam;
            return TutorialPresentationStateStore.Instance.AddKeyControlHint(
                tutorial.TutorialGuid.ToString(), param.KeyName, param.UiState);
        }
    }
}
