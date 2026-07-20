using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Client.Skit.UI
{
    public class BackgroundSkitUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text skitText;
        
        [SerializeField] private AudioSource voiceSource;
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        // 文字表示だけを切り替える（webモード抑止用。ルートを消すとAudioSourceの音声再生が止まるため分離）
        // Toggle only the text visuals (for web-mode suppression; disabling the root would kill AudioSource playback)
        public void SetTextVisible(bool visible)
        {
            skitText.gameObject.SetActive(visible);
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
