using System;
using System.Collections.Generic;
using Client.Mod.Texture;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    /// 接続ツールのアイコン画像をconnectToolGuidで管理するクラス
    /// Manages connect-tool icon images keyed by connectToolGuid
    /// </summary>
    public class ConnectToolImageContainer
    {
        private readonly Dictionary<Guid, ItemViewData> _connectToolImageList;

        private ConnectToolImageContainer(Dictionary<Guid, ItemViewData> connectToolImageList)
        {
            _connectToolImageList = connectToolImageList;
        }

        public static ConnectToolImageContainer CreateAndLoadConnectToolImageContainer(string modsDirectory)
        {
            var connectToolImageList = ConnectToolTextureLoader.GetConnectToolTexture(modsDirectory);

            return new ConnectToolImageContainer(connectToolImageList);
        }

        public ItemViewData GetConnectToolView(Guid connectToolGuid)
        {
            if (_connectToolImageList.TryGetValue(connectToolGuid, out var view)) return view;

            Debug.LogError($"ConnectTool ItemViewData not found. connectToolGuid:{connectToolGuid}");
            return null;
        }
    }
}
