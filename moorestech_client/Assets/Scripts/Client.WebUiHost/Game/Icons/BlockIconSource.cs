using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// GET /api/block-icons/{blockId}.png でブロックアイコンを解決する
    /// Resolves block icons served at GET /api/block-icons/{blockId}.png
    /// </summary>
    public class BlockIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/block-icons/";

        public string PathPrefix => PathPrefixConst;

        public bool IsReady => ClientContext.BlockImageContainer != null;

        public bool IsValidKey(string keyText)
        {
            return int.TryParse(keyText, out _);
        }

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!int.TryParse(keyText, out var blockIdValue)) return null;

            var view = ClientContext.BlockImageContainer.GetBlockView(new BlockId(blockIdValue));
            return view?.ItemTexture as Texture2D;
        }
    }
}
