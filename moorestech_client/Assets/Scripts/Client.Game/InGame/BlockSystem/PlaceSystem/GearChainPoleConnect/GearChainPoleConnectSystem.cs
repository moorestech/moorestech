using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Input;
using UnityEngine;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleの接続システム
    /// GearChainPole connection system
    /// </summary>
    public class GearChainPoleConnectSystem : IPlaceSystem
    {
        private readonly Camera _mainCamera;

        // 接続元のGearChainPole
        // Source GearChainPole for connection
        private IGearChainPoleConnectAreaCollider _connectFromPole;

        public GearChainPoleConnectSystem(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }

        public void Enable()
        {
            // 接続元の選択状態をリセットする
            // Reset source selection state
            _connectFromPole = null;
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 接続元が未選択の場合、接続元を選択する
            // If source is not selected, select the source
            if (_connectFromPole == null)
            {
                SelectSourcePole();
                return;
            }

            // 接続先を選択して接続を実行する
            // Select target and execute connection
            SelectTargetAndConnect(context);

            #region Internal

            void SelectSourcePole()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;

                var collider = GetGearChainPoleCollider();
                if (collider == null) return;

                _connectFromPole = collider;
                Debug.Log($"[GearChainPoleConnect] Source pole selected: {_connectFromPole.GetBlockPosition()}");
            }

            void SelectTargetAndConnect(PlaceSystemUpdateContext ctx)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;

                // 接続先を取得する
                // Get connection target
                var connectToPole = GetGearChainPoleCollider();
                if (connectToPole == null) return;

                // 自身への接続は無視する
                // Ignore self-connection
                var fromPos = _connectFromPole.GetBlockPosition();
                var toPos = connectToPole.GetBlockPosition();
                if (fromPos == toPos)
                {
                    Debug.Log("[GearChainPoleConnect] Connection to the same block was ignored");
                    return;
                }

                // 接続プロトコルを送信する
                // Send connection protocol
                Debug.Log($"[GearChainPoleConnect] Executing connection: {fromPos} -> {toPos}");
                ClientContext.VanillaApi.SendOnly.ConnectGearChain(fromPos, toPos, ctx.HoldingItemId);

                // 接続元の選択をリセットする
                // Reset source selection
                _connectFromPole = null;
            }

            IGearChainPoleConnectAreaCollider GetGearChainPoleCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IGearChainPoleConnectAreaCollider>(
                    _mainCamera,
                    out var collider,
                    Without_Player_MapObject_BlockBoundingBox_LayerMask);
                return collider;
            }

            #endregion
        }

        public void Disable()
        {
            // 無効化時に選択状態をクリアする
            // Clear selection state on disable
            _connectFromPole = null;
        }
    }
}
