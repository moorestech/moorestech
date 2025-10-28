using System.Collections.Generic;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    public interface IPreviewOnlyObject
    {
        public void Initialize(BlockId blockId);
        public void SetActive(bool active);
        public void SetEnableRenderers(bool enable); // これいらないのでは？消すかどうか検討する
    }
    
    public class PreviewOnlyObject : MonoBehaviour, IPreviewOnlyObject
    {
        private readonly List<Renderer> _renderers = new();
        
        public void Initialize(BlockId blockId)
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