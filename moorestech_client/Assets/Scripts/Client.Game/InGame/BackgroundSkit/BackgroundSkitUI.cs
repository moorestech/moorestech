using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.BackgroundSkit
{
    public class BackgroundSkitUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private TMP_Text line;

        [SerializeField] private AudioSource voiceSource;

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public async UniTask SetText(string name, string sentence, AudioClip voice = null)
        {
            characterName.text = name;
            line.text = sentence;

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