using System;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// GET /api/fluid-icons/{guid}.png で液体アイコンを解決する
    /// Resolves fluid icons served at GET /api/fluid-icons/{guid}.png
    /// </summary>
    public class FluidIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/fluid-icons/";

        public string PathPrefix => PathPrefixConst;

        public bool IsReady => ClientContext.FluidImageContainer != null;

        public bool IsValidKey(string keyText)
        {
            return Guid.TryParse(keyText, out _);
        }

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!Guid.TryParse(keyText, out var fluidGuid)) return null;

            var view = ClientContext.FluidImageContainer.GetItemView(fluidGuid);
            return view?.FluidTexture as Texture2D;
        }
    }
}
