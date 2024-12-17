using Client.CutScene;
using Client.Game.Skit;
using Client.Game.Skit.Starter;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace Client.Game.Common
{
    public class SkitFireManager : MonoBehaviour
    {
        [SerializeField] private PlayerSkitStarterDetector playerSkitStarterDetector;
        [SerializeField] private SkitManager skitManager;
        
        
        [SerializeField] private TimelinePlayer timelinePlayer; // TODO こういうのは全部やめてマスタで管理するようにしたい
        [SerializeField] private PlayableAsset trailerMovie;
        
        
        private void Update()
        {
            if (playerSkitStarterDetector.IsStartReady && UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                PlayCutscene().Forget();
            }
        }
        
        private async UniTask PlayStory() // TODo トレイラー対応のために仮でこのメソッドを使っていない
        {
            GameStateController.ChangeState(GameStateType.Skit);
            
            var csv = playerSkitStarterDetector.CurrentSkitStarterObject.ScenarioCsv;
            await skitManager.StartSkit(csv);
            
            GameStateController.ChangeState(GameStateType.InGame);
        }
        
        
        
        private async UniTask PlayCutscene()
        {
            GameStateController.ChangeState(GameStateType.CutScene);
            
            await timelinePlayer.Play(trailerMovie);
            
            GameStateController.ChangeState(GameStateType.InGame);
        }
    }
}