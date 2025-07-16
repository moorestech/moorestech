using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Skit.Skit
{
    /// <summary>
    /// Animation コンポーネント（Legacy）を使って
    /// ・Addressables から読み込んだ AnimationClip を登録  
    /// ・前のモーションから fadeDuration 秒でクロスフェード  
    /// ・以降はループ再生  
    /// を行うユーティリティ。
    /// </summary>
    public class SkitCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animation animation;   // Animation（Legacy）

        /// <summary>現在ループ再生中のクリップ</summary>

        public void Initialize()
        {
            animation.playAutomatically = false;    // 明示的に制御
        }

        /// <summary>
        /// animationId の AnimationClip を Addressables からロードし、
        /// fadeDuration 秒でクロスフェードしてループ再生する。
        /// </summary>
        /// <param name="animationId">Addressable のキー</param>
        /// <param name="fadeDuration">クロスフェード時間（秒）。0 以下なら即時切替</param>
        public async UniTask PlayAnimation(string animationId, float fadeDuration = 0.25f)
        {
            var clip = await AddressableLoader.LoadAsyncDefault<AnimationClip>(animationId);
            if (clip == null)
            {
                Debug.LogError($"{nameof(SkitCharacterAnimator)} : AnimationClip '{animationId}' が見つかりません");
                return;
            }

            // ② 未登録なら Animation に追加
            if (!animation.GetClip(clip.name))
            {
#if UNITY_EDITOR
                // 実行時でも書き換え可能だが念のため
                if (!clip.legacy) clip.legacy = true;  // レガシー扱いに
#endif
                clip.wrapMode = WrapMode.Loop;         // ループ再生
                animation.AddClip(clip, clip.name);
            }
            else
            {
                // 既登録でもラップモードを上書きしておく（Import 設定漏れ対策）
                animation.GetClip(clip.name).wrapMode = WrapMode.Loop;
            }

            // ③ クロスフェード／即時再生
            if (fadeDuration <= 0f || animation.clip == null)
            {
                animation.Play(clip.name);
            }
            else
            {
                animation.CrossFade(clip.name, fadeDuration, PlayMode.StopSameLayer);
            }
        }
    }
}
