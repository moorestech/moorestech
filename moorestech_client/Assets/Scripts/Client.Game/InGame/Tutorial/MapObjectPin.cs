using System;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    public class MapObjectPin : MonoBehaviour
    {
        [SerializeField] private InGameCameraController inGameCameraController;
        [SerializeField] private TMP_Text pinText;
        
        private void Update()
        {
            // Y軸を常にカメラに向ける
            transform.LookAt(inGameCameraController.Position);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        }
        
        public void SetText(string text)
        {
            pinText.text = text;
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
    }
}