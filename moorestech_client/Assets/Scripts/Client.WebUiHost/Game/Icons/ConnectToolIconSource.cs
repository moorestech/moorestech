using System;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// GET /api/connect-tool-icons/{guid}.png で接続ツールアイコンを解決する
    /// Resolves connect-tool icons served at GET /api/connect-tool-icons/{guid}.png
    /// </summary>
    public class ConnectToolIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/connect-tool-icons/";

        public string PathPrefix => PathPrefixConst;

        public bool IsReady => ClientContext.ConnectToolImageContainer != null;

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!Guid.TryParse(keyText, out var connectToolGuid)) return null;

            var view = ClientContext.ConnectToolImageContainer.GetConnectToolView(connectToolGuid);
            return view?.ItemTexture as Texture2D;
        }
    }
}
