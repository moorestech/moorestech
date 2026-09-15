using Client.Game.Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class QuitGame : MonoBehaviour
    {
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            // 終了はすべて終了パイプラインを通す。直接Application.Quitすると意図した終了であることが誰にも表明されない
            // Every exit goes through the shutdown pipeline; a direct Application.Quit declares the intent to no one
            quitButton.onClick.AddListener(() => GameShutdownEvent.QuitApplicationAsync().Forget());
        }
    }
}
