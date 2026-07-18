using System;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.Playables;

namespace Client.CutScene
{
    public class TimelinePlayer : MonoBehaviour
    {
        // 再生中フラグの変化通知。ゲーム全体状態への反映はClient.Game側が購読して行う（逆参照は循環になるため禁止）
        // Playing-flag change stream; Client.Game subscribes to map it onto the game state (a direct back-reference would be a cycle)
        private static readonly Subject<bool> _onPlayingChanged = new();
        public static IObservable<bool> OnPlayingChanged => _onPlayingChanged;

        [SerializeField] private PlayableDirector playableDirector;

        public async UniTask Play(PlayableAsset playableAsset)
        {
            _onPlayingChanged.OnNext(true);
            playableDirector.playableAsset = playableAsset;
            playableDirector.Play();

            await UniTask.WaitUntil(() => playableDirector.state != PlayState.Playing);

            playableDirector.playableAsset = null;
            _onPlayingChanged.OnNext(false);
        }
    }
}
