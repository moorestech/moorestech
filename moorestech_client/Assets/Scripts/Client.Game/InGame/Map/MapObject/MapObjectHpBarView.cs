using System;
using Client.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectHpBarView : MonoBehaviour
    {
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMP_Text hpText;
        
        public void SetHp(float hp, float maxHp)
        {
            hpSlider.value = hp / maxHp;
            hpText.text = $"{hp}/{maxHp}";
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        // 個体スケールがUI表示に波及しないよう、親のlossyScaleを打ち消してワールド等倍にする
        // Counter-scales against the parent's lossyScale so this bar renders at world unit scale regardless of the instance scale
        public void SetWorldUnitScale()
        {
            var lossy = transform.parent.lossyScale;
            transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
        }
        
        private void Update()
        {
            var currentCamera = CameraManager.MainCamera.Camera;
            
            if (currentCamera)
            {
                transform.LookAt(transform.position + currentCamera.transform.rotation * Vector3.forward, currentCamera.transform.rotation * Vector3.up);
                var currentRotation = transform.localRotation.eulerAngles;
                currentRotation.x = 0;
                currentRotation.z = 0;
                transform.localRotation = Quaternion.Euler(currentRotation);
            }
        }
    }
}