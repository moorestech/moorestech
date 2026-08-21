using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// GET /api/icons/{itemId}.png でアイテムアイコンを解決する
    /// Resolves item icons served at GET /api/icons/{itemId}.png
    /// </summary>
    public class ItemIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/icons/";

        public string PathPrefix => PathPrefixConst;

        public bool IsReady => ClientContext.ItemImageContainer != null;

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!int.TryParse(keyText, out var itemIdValue)) return null;

            var view = ClientContext.ItemImageContainer.GetItemView(new ItemId(itemIdValue));
            return view?.ItemTexture as Texture2D;
        }
    }
}
