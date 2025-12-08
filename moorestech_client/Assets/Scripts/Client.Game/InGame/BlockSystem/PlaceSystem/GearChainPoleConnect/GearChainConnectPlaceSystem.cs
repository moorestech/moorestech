using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Input;
using UnityEngine;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPole間の接続を操作するPlaceSystem
    /// PlaceSystem for managing connections between GearChainPoles
    /// </summary>
    public class GearChainConnectPlaceSystem : IPlaceSystem
    {
        private readonly Camera _mainCamera;
        private readonly GearChainConnectPreviewObject _previewObject;
        private readonly GearChainConnectRangeObject _rangeObject;

        private IGearChainPoleConnectAreaCollider _connectFromPole;

        public GearChainConnectPlaceSystem(
            Camera mainCamera,
            GearChainConnectPreviewObject previewObject,
            GearChainConnectRangeObject rangeObject)
        {
            _mainCamera = mainCamera;
            _previewObject = previewObject;
            _rangeObject = rangeObject;
        }

        public void Enable()
        {
            // 選択状態をリセットする
            // Reset selection state
            _connectFromPole = null;
            _previewObject.SetActive(false);
            _rangeObject.SetActive(false);
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // キャンセル入力を処理する
            // Process cancel input
            if (CheckCancelInput())
            {
                CancelSelection();
                return;
            }

            // 接続元が未選択なら接続元を選択する
            // Select source pole if not selected
            if (_connectFromPole == null)
            {
                TrySelectFromPole();
                return;
            }

            // 接続先を検出してプレビューを更新する
            // Detect target pole and update preview
            UpdateConnectionPreview(context);

            #region Internal

            bool CheckCancelInput()
            {
                return InputManager.Playable.ScreenRightClick.GetKeyDown ||
                       UnityEngine.Input.GetKeyDown(KeyCode.Escape);
            }

            void CancelSelection()
            {
                _connectFromPole = null;
                _previewObject.SetActive(false);
                _rangeObject.SetActive(false);
            }

            void TrySelectFromPole()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;

                var detectedPole = GetGearChainPoleCollider();
                if (detectedPole == null) return;

                _connectFromPole = detectedPole;

                // 範囲表示を有効化する
                // Enable range display
                var poleWorldPos = GetPoleWorldPosition(detectedPole);
                _rangeObject.SetPosition(poleWorldPos);
                _rangeObject.SetRange(detectedPole.MaxConnectionDistance);
                _rangeObject.SetActive(true);
            }

            void UpdateConnectionPreview(PlaceSystemUpdateContext ctx)
            {
                var toPole = GetGearChainPoleCollider();
                var fromWorldPos = GetPoleWorldPosition(_connectFromPole);

                // カーソル位置またはポール位置をターゲットとする
                // Use cursor position or pole position as target
                Vector3 toWorldPos;
                GearChainConnectionState connectionState;
                int requiredChainCount;
                bool hasEnoughChainItems;

                if (toPole == null || toPole == _connectFromPole)
                {
                    // カーソル位置に向かってプレビューラインを表示する
                    // Show preview line towards cursor position
                    if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out toWorldPos, out _))
                    {
                        _previewObject.SetActive(false);
                        return;
                    }

                    connectionState = GearChainConnectionState.NotConnectable;
                    requiredChainCount = GearChainCostCalculator.CalculateRequiredChainCount(
                        Vector3.Distance(fromWorldPos, toWorldPos), ctx.HoldingItemId);
                    hasEnoughChainItems = false;
                }
                else
                {
                    // ポール間の接続状態を判定する
                    // Determine connection state between poles
                    toWorldPos = GetPoleWorldPosition(toPole);
                    connectionState = DetermineConnectionState(_connectFromPole, toPole);
                    requiredChainCount = GearChainCostCalculator.CalculateRequiredChainCount(
                        _connectFromPole.Position, toPole.Position, ctx.HoldingItemId);

                    // インベントリ確認は省略（サーバー側で判定）
                    // Skip inventory check (server-side validation)
                    hasEnoughChainItems = true;
                }

                // プレビューを更新する
                // Update preview
                var previewData = new GearChainConnectPreviewData(
                    fromWorldPos, toWorldPos, connectionState, requiredChainCount, hasEnoughChainItems);
                _previewObject.ShowPreview(previewData);
                _previewObject.SetActive(true);

                // 左クリックで接続または切断を実行する
                // Execute connect or disconnect on left click
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && toPole != null && toPole != _connectFromPole)
                {
                    ExecuteConnectionAction(toPole, connectionState, ctx);
                }
            }

            GearChainConnectionState DetermineConnectionState(
                IGearChainPoleConnectAreaCollider from,
                IGearChainPoleConnectAreaCollider to)
            {
                // 既に接続済みかどうか確認する
                // Check if already connected
                if (from.ContainsConnection(to.Position) || to.ContainsConnection(from.Position))
                {
                    return GearChainConnectionState.AlreadyConnected;
                }

                // 接続数上限を確認する
                // Check connection count limits
                if (from.IsConnectionFull || to.IsConnectionFull)
                {
                    return GearChainConnectionState.NotConnectable;
                }

                // 距離を確認する
                // Check distance
                var distance = Vector3Int.Distance(from.Position, to.Position);
                var maxDistance = Math.Min(from.MaxConnectionDistance, to.MaxConnectionDistance);
                if (distance > maxDistance)
                {
                    return GearChainConnectionState.NotConnectable;
                }

                return GearChainConnectionState.Connectable;
            }

            void ExecuteConnectionAction(
                IGearChainPoleConnectAreaCollider toPole,
                GearChainConnectionState state,
                PlaceSystemUpdateContext ctx)
            {
                switch (state)
                {
                    case GearChainConnectionState.Connectable:
                        // 接続リクエストを送信する
                        // Send connect request
                        ClientContext.VanillaApi.SendOnly.ConnectGearChain(
                            _connectFromPole.Position, toPole.Position, ctx.HoldingItemId);
                        break;

                    case GearChainConnectionState.AlreadyConnected:
                        // 切断リクエストを送信する
                        // Send disconnect request
                        ClientContext.VanillaApi.SendOnly.DisconnectGearChain(
                            _connectFromPole.Position, toPole.Position);
                        break;

                    default:
                        return;
                }

                // 選択状態をリセットする
                // Reset selection state
                _connectFromPole = null;
                _previewObject.SetActive(false);
                _rangeObject.SetActive(false);
            }

            IGearChainPoleConnectAreaCollider GetGearChainPoleCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IGearChainPoleConnectAreaCollider>(
                    _mainCamera, out var collider, Without_Player_MapObject_BlockBoundingBox_LayerMask);
                return collider;
            }

            Vector3 GetPoleWorldPosition(IGearChainPoleConnectAreaCollider pole)
            {
                // ブロック中心のワールド座標を返す
                // Return world coordinates of block center
                return pole.Position + new Vector3(0.5f, 0.5f, 0.5f);
            }

            #endregion
        }

        public void Disable()
        {
            // 無効化時に状態をリセットする
            // Reset state when disabled
            _connectFromPole = null;
            _previewObject.SetActive(false);
            _rangeObject.SetActive(false);
        }
    }
}
