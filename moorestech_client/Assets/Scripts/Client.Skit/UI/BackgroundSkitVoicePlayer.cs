using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Skit.UI
{
    /// <summary>
    ///     背景スキットの音声再生。文字表示は Web が SkitPresentationStateStore 経由で描く
    ///     Voice playback for background skits; the web renders the text through SkitPresentationStateStore
    /// </summary>
    public class BackgroundSkitVoicePlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource voiceSource;

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public async UniTask PlayVoiceAndWait(AudioClip voice)
        {
            if (voice == null)
            {
                await UniTask.Delay(3000);
                return;
            }

            voiceSource.clip = voice;
            voiceSource.Play();

            await UniTask.Delay((int)(voiceSource.clip.length * 1000));
        }
    }
}
