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
            var sprite = _blockIconImagePhotographer.GetIcon(_blockPrefab);
            _image.sprite = sprite;
        }
    }
}