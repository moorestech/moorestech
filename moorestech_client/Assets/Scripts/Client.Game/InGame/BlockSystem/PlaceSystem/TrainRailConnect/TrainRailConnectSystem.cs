using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Core.Master;
using Game.Construction;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Common.LayerConst;
using static Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.TrainRailConnectPreviewCalculator;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class TrainRailConnectSystem : PlaceSystemBase<ConnectToolPlacementTarget>
    {
        private readonly RailConnectPreviewObject _previewObject;
        private readonly Camera _mainCamera;
        private readonly RailGraphClientCache _cache;
        private readonly ILocalPlayerInventory _playerInventory;
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly TrainRailConnectRequestSender _requestSender;
        private IRailComponentConnectAreaCollider _connectFromArea;

        public TrainRailConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController controller, RailConnectPreviewObject previewObject, RailGraphClientCache cache, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore, ConstructionWalletQuery constructionWalletQuery)
        {
            _mainCamera = mainCamera;
            _previewObject = previewObject;
            _cache = cache;
            _playerInventory = localPlayerInventory.LocalPlayerInventory;
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, controller);
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _constructionWalletQuery = constructionWalletQuery;
            _requestSender = new TrainRailConnectRequestSender(cache, blockGameObjectDataStore);
        }
        public override void Enable()
        {
            ResetState();
        }

        protected override void ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            _trainRailPlaceSystemService.Disable();
            // 取込: 前フレームまでの橋脚設置応答を接続元へ反映する
            // Consume: apply pier placement responses up to the previous frame to the origin
            if (_requestSender.TryConsumePlacedPierArea(out var placedPierArea)) _connectFromArea = placedPierArea;

            // 接続元が未選択なら接続元を選択する
            // If the connection source is not selected, select the connection source.
            if (_connectFromArea == null)
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi())
                {
                    _connectFromArea = GetTrainRailConnectAreaCollider();
                }
                if (_connectFromArea != null)
                {
                    var destination = _connectFromArea.CreateConnectionDestination();
                    var componentPosition = destination.blockPosition;
                    Debug.Log($"[TrainRailConnect] Select FROM: IsFront={_connectFromArea.IsFront} pos=({componentPosition.x},{componentPosition.y},{componentPosition.z})");
                }
                return;
            }
            // Compute ConnectionDestination for both endpoints
            var fromDestination = _connectFromArea.CreateConnectionDestination();

            // 選択中のレールconnectToolのGuidを使う
            // Use the selected rail connectTool's Guid
            var connectToolGuid = target.ConnectToolGuid;

            // If the connection point is not under the cursor, return.
            var connectToArea = GetTrainRailConnectAreaCollider();
            if (connectToArea == null)
            {
                if (PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var position, out _))
                {
                    _trainRailPlaceSystemService.Enable();

                    if (!ConnectToolCatalog.TryGetPlaceBlock(ConnectToolType.TrainRailConnect, out var pierBlockId, out var pierBlockMaster))
                    {
                        // 橋脚未定義の場合は設置不可。仮にデフォルトの最大長で判定する
                        // No pier defined: still preview with default max length
                        ShowPreview(CalculatePreviewData(fromDestination, position, _trainRailPlaceSystemService.RailDirection, _cache, _playerInventory, _blockGameObjectDataStore, float.MaxValue, connectToolGuid, Array.Empty<ConstructionRequiredItemElement>(), 0));
                    }
                    else
                    {
                        // 橋脚がある場合は設置可能。配置予定の TrainRail ブロックの最大長を参照する
                        // Pier available: pass the placing TrainRail block's max length
                        var pierMaxLength = TrainRailConnectPreviewCalculator.GetMaxConnectableRailLength(pierBlockMaster);
                        var placeInfo = _trainRailPlaceSystemService.ManualUpdate(pierBlockId, feedback);

                        // 距離外でピアが立たないならConnectorPositionが古いので接続プレビューごと止める（理由行はサービスが積み済み）
                        // No pier means a stale ConnectorPosition, so stop the connect preview too (the service already pushed the reason)
                        if (placeInfo == null) { _previewObject.SetActive(false); return; }

                        // 橋脚の建設コストはレール素材より先に消費されるため、予約と橋脚自身の可否の双方をコストセット数から導く（サーバーのRailConnectWithPlacePierProtocolと同じ関門）
                        // The pier's construction cost is consumed before the rail materials, so both the reservation and the pier's own affordability derive from the cost-set count (the same gate as the server's RailConnectWithPlacePierProtocol)
                        var previewData = CalculatePreviewData(fromDestination, _trainRailPlaceSystemService.ConnectorPosition, _trainRailPlaceSystemService.RailDirection, _cache, _playerInventory, _blockGameObjectDataStore, pierMaxLength, connectToolGuid, pierBlockMaster.RequiredItems, _constructionWalletQuery.GetRequiredCostSets(pierBlockId, 1));
                        ShowPreview(previewData);

                        // 地面干渉・橋脚コスト不足・レール判定不可のいずれでも送らない（サーバーが拒否する組み合わせをここで塞ぐ）
                        // Never send on terrain block, pier cost shortage or a failed rail judgement, closing every combination the server would reject
                        if (!placeInfo.Placeable || !previewData.IsPlaceable) return;
                        SendConnectRailWithPlacePierProtocol(placeInfo, previewData.RailTypeGuid, pierBlockId);
                    }
                }
            }
            else
            {
                var toDestination = connectToArea.CreateConnectionDestination();
                toDestination.IsFront = !toDestination.IsFront;
                if (fromDestination.IsDefault() || toDestination.IsDefault())
                {
                    Debug.LogWarning("[TrainRailConnect] Invalid destination detected. Re-select connection target.");
                    _previewObject.SetActive(false);
                    return;
                }
                if (!TryResolveNode(fromDestination, out var fromNode) ||
                    !TryResolveNode(toDestination, out var toNode))
                {
                    Debug.LogWarning("[TrainRailConnect] Failed to resolve node info from cache.");
                    _connectFromArea = null;
                    return;
                }
                var previewData = CalculatePreviewData(fromDestination, toDestination, _cache, _playerInventory, _blockGameObjectDataStore, connectToolGuid);
                ShowPreview(previewData);
                if (!previewData.IsPlaceable) return;
                SendConnectRailProtocol(fromNode, toNode, previewData.RailTypeGuid);
            }
            #region Internal
            void ShowPreview(TrainRailConnectPreviewData previewData)
            {
                if (!previewData.IsValid)
                {
                    _previewObject.SetActive(false);
                    return;
                }
                _previewObject.SetActive(true);
                _previewObject.ShowPreview(previewData);
                TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);
            }
            void SendConnectRailProtocol(IRailNode from, IRailNode to, Guid railTypeGuid)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown || UiPointerHitTest.IsPointerOverAnyUi()) return;
                _previewObject.SetActive(false);
                Debug.Log($"Connecting rails: From NodeId={from.NodeId}, Guid={from.NodeGuid} To NodeId={to.NodeId}, Guid={to.NodeGuid}");
                ClientContext.VanillaApi.SendOnly.ConnectRail(from.NodeId, from.NodeGuid, to.NodeId, to.NodeGuid, railTypeGuid);
                _connectFromArea = null;
            }
            void SendConnectRailWithPlacePierProtocol(PlaceInfo placeInfo, Guid railTypeGuid, BlockId pierBlockId)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown || UiPointerHitTest.IsPointerOverAnyUi()) return;
                if (!TryResolveNode(fromDestination, out var fromNode)) return;
                _previewObject.SetActive(false);
                // 接続元は応答が引き継ぎ先を確定するまで空にする（引き継ぎは次フレーム以降の取込で行う）
                // Clear the origin until the response settles the hand-off, which lands via consumption on a later frame
                _connectFromArea = null;
                _requestSender.SendPlacePierAndConnect(fromNode, pierBlockId, placeInfo, railTypeGuid);
            }
            IRailComponentConnectAreaCollider GetTrainRailConnectAreaCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IRailComponentConnectAreaCollider>(_mainCamera, out var connectArea, Without_Player_MapObject_BlockBoundingBox_LayerMask);
                return connectArea;
            }
            bool TryResolveNode(ConnectionDestination destination, out IRailNode railNode)
            {
                railNode = null;
                return _cache.TryGetNodeId(destination, out var nodeId) && _cache.TryGetNode(nodeId, out railNode);
            }
            #endregion
        }
        // 右短押しで接続の起点だけを解除し、起点基準のプレビューも消す。起点が無ければ解除対象なし
        // A right short press releases only the connection origin and hides its preview; without an origin there is nothing to cancel
        public override bool TryCancelInProgressOperation()
        {
            // 飛行中の橋脚リクエストは可視の進行中操作に数えないが、遅着の起点復活だけは必ず断つ
            // An in-flight pier request is not a visible in-progress operation, but its late origin write-back must always be cut off
            _requestSender.Invalidate();

            if (_connectFromArea == null) return false;

            _connectFromArea = null;
            _previewObject.SetActive(false);
            return true;
        }

        public override void Disable()
        {
            ResetState();
            _previewObject.SetActive(false);
            _trainRailPlaceSystemService.Disable();
        }

        private void ResetState()
        {
            _requestSender.Invalidate();
            _connectFromArea = null;
        }
    }
}
