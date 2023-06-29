using Cysharp.Threading.Tasks;
using MainGame.Localization;
using UnityEngine;

namespace MainGame.UnityView.UI.Mission
{
    public class MissionBarUIElement : MonoBehaviour
    {
        [SerializeField] private GameObject doneImage;
        [SerializeField] private TextMeshProLocalize missionName;


        public void SetMissionNameKey(string key)
        {
            missionName.SetKey(key);
        }
        
        public async UniTask SetDone()
        {
            SetActive(true);
            doneImage.SetActive(true);
            await UniTask.Delay(10000);
        }
        
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}