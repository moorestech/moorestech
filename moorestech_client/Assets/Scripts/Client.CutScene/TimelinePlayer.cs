using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using Client.Game.Common;

namespace Client.CutScene
{
    public class TimelinePlayer : MonoBehaviour
    {
        [SerializeField] private PlayableDirector playableDirector;
        
        public async UniTask Play(PlayableAsset playableAsset)
        {
            GameStateController.ChangeState(GameStateType.CutScene);
            playableDirector.playableAsset = playableAsset;
            playableDirector.Play();
            
            await UniTask.WaitUntil(() => playableDirector.state != PlayState.Playing);
            
            playableDirector.playableAsset = null;
            GameStateController.ChangeState(GameStateType.InGame);
        }
    }
}
