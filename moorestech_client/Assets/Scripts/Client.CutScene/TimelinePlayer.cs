using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace Client.CutScene
{
    public class TimelinePlayer : MonoBehaviour
    {
        [SerializeField] private PlayableDirector playableDirector;
        
        public async UniTask Play(PlayableAsset playableAsset)
        {
            playableDirector.playableAsset = playableAsset;
            playableDirector.Play();
            
            await UniTask.WaitUntil(() => playableDirector.state != PlayState.Playing);
            
            playableDirector.playableAsset = null;
        }
    }
}