using Client.Game.Skit;
using Client.Game.Skit.Starter;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.Common
{
    public class SkitFireManager : MonoBehaviour
    {
        [SerializeField] private PlayerSkitStarterDetector playerSkitStarterDetector;
        [SerializeField] private SkitManager skitManager;

        private void Update()
        {
            if (playerSkitStarterDetector.IsStartReady && UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                PlayStory().Forget();
            }
        }

        private async UniTask PlayStory()
        {
            GameStateController.ChangeState(GameStateType.Skit);

            var csv = playerSkitStarterDetector.CurrentSkitStarterObject.ScenarioCsv;
            await skitManager.StartSkit(csv);

            GameStateController.ChangeState(GameStateType.InGame);
        }
    }
}