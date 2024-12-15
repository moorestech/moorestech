using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographerTest : MonoBehaviour
    {
        [SerializeField] private BlockIconImagePhotographer _blockIconImagePhotographer;
        [SerializeField] private GameObject _blockPrefab;
        
        [SerializeField] private Image _image;
        
        private void Start()
        {
            TakeScreenShot().Forget();
        }
        
        private async UniTask TakeScreenShot()
        {
            var sprite = await _blockIconImagePhotographer.GetIcon(_blockPrefab);
            _image.sprite = sprite;
        }
        
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                TakeScreenShot().Forget();
            }
        }
    }
}