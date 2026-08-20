using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    // キー操作ヒントはWebの下中央HUDが描く。uiState一致の判定もWeb側なので、ここはマスタ値をそのまま公開するだけ
    // Key-control hints are drawn by the web's bottom-center HUD; uiState matching is web-side too, so this only publishes master values
    public class KeyControlTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (KeyControlTutorialParam)tutorial.TutorialParam;
            return TutorialPresentationStateStore.Instance.AddKeyControlHint(
                tutorial.TutorialGuid.ToString(), param.KeyName, param.UiState);
        }
    }
}
