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