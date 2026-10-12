using System;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    ///     BP撮影済みアイコンの解決
    ///     Resolves photographed blueprint icons
    /// </summary>
    public class BlueprintIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/blueprint-icons/";
        public string PathPrefix => PathPrefixConst;
        public bool IsReady => ClientDIContext.BlueprintThumbnailLookup != null;

        public bool IsValidKey(string keyText)
        {
            return Guid.TryParse(keyText, out _);
        }

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!Guid.TryParse(keyText, out var guid)) return null;
            return ClientDIContext.BlueprintThumbnailLookup.TryGet(guid, out var thumbnail) ? thumbnail : null;
        }
    }
}
