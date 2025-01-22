using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PreviewOnlyObject : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new();
        
        public void Initialize()
        {
            _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetEnableRenderers(bool enable)
        {
            foreach (var r in _renderers) r.enabled = enable;
        }
    }
}