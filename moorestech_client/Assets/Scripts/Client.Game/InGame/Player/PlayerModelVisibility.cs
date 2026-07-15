using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Player
{
    public class PlayerModelVisibility
    {
        private readonly Dictionary<Renderer, bool> _enabledStateBeforeHide = new();
        public void Hide(Renderer[] renderers)
        {
            foreach (var modelRenderer in renderers)
            {
                if (!_enabledStateBeforeHide.ContainsKey(modelRenderer)) _enabledStateBeforeHide.Add(modelRenderer, modelRenderer.enabled);
                modelRenderer.enabled = false;
            }
        }

        public void Restore()
        {
            foreach (var (modelRenderer, wasEnabled) in _enabledStateBeforeHide)
            {
                if (modelRenderer != null) modelRenderer.enabled = wasEnabled;
            }
            _enabledStateBeforeHide.Clear();
        }
    }
}
