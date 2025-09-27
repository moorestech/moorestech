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
        
        public async UniTask SetText(string characterName, string body, AudioClip voice = null)
        {
            skitText.text = $"{characterName} : {body}";
            
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