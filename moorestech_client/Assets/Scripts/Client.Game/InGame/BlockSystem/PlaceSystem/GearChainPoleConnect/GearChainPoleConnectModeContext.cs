using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// 歯車チェーンポール接続システムの各モードが共有する状態と共通操作。
    /// 起点ポールの選択状態は全モード共通で、モード切替をまたいで維持される。
    /// State and common operations shared by all modes of the gear chain pole connect system.
    /// The source pole selection is shared across modes and survives mode switches.
    /// </summary>
    public class GearChainPoleConnectModeContext
    {
        public readonly Camera MainCamera;
        public readonly ILocalPlayerInventory PlayerInventory;
        public readonly BlockGameObjectDataStore BlockGameObjectDataStore;
        public readonly GearChainPoleExtendPreviewObject PreviewObject;
        public readonly GearChainPoleExtendRequestSender RequestSender;

        // 接続元のGearChainPole
        // Source GearChainPole for connection
        public IGearChainPoleConnectAreaCollider ConnectFromPole { get; private set; }

        public GearChainPoleConnectModeContext(Camera mainCamera, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore, GearChainPoleExtendPreviewObject previewObject, GearChainPoleExtendRequestSender requestSender)
        {
            MainCamera = mainCamera;
            PlayerInventory = playerInventory;
            BlockGameObjectDataStore = blockGameObjectDataStore;
            PreviewObject = previewObject;
            RequestSender = requestSender;
        }

        public void SetConnectFromPole(IGearChainPoleConnectAreaCollider pole)
        {
            ConnectFromPole = pole;
        }

        /// <summary>
        /// クリックした既存ポールを起点として選択し、進行中の応答を無効化する
        /// Select the clicked existing pole as the source and invalidate pending responses
        /// </summary>
        public void SelectSourcePole(IGearChainPoleConnectAreaCollider pole)
        {
            ConnectFromPole = pole;
            RequestSender.Invalidate();
        }

        /// <summary>
        /// 起点選択・プレビュー・進行中の応答をすべてクリアする
        /// Clear source selection, preview and pending responses
        /// </summary>
        public void Reset()
        {
            ConnectFromPole = null;
            PreviewObject.Hide();
            RequestSender.Invalidate();
        }

        public bool IsScreenClicked()
        {
            // UI上のクリックはブロック設置操作として扱わない
            // Ignore clicks over UI as placement input
            return InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject();
        }

        public IGearChainPoleConnectAreaCollider GetGearChainPoleCollider()
        {
            PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IGearChainPoleConnectAreaCollider>(
                MainCamera,
                out var collider,
                Without_Player_MapObject_BlockBoundingBox_LayerMask);
            return collider;
        }
    }
}
