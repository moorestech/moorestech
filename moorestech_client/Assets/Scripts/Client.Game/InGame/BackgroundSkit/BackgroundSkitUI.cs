using System.Collections.Generic;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.BackgroundSkit
{
    public class BackgroundSkitUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text skitText;
        
        [SerializeField] private AudioSource voiceSource;
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
        public async UniTask SetText(string name, string sentence, AudioClip voice = null)
        {
            skitText.text = $"{name} : {sentence}";
            
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