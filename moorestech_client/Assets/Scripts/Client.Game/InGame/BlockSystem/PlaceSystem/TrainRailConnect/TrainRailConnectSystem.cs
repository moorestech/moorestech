using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train;
using Client.Input;
using Game.Train.RailGraph;
using UnityEngine;
using static Client.Common.LayerConst;
using static Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.TrainRailConnectPreviewCalculator;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class TrainRailConnectSystem : IPlaceSystem
    {
        private readonly RailConnectPreviewObject _previewObject;
        private readonly Camera _mainCamera;
        private readonly RailGraphClientCache _cache;
        
        private IRailComponentConnectAreaCollider _connectFromArea;
        public TrainRailConnectSystem(Camera mainCamera, RailConnectPreviewObject previewObject, RailGraphClientCache cache)
        {
            _mainCamera = mainCamera;
            _previewObject = previewObject;
            _cache = cache;
        }
        
        public void Enable()
        {
            _connectFromArea = null;
        }
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 接続元が未選択なら接続元を選択する
            // If the connection source is not selected, select the connection source.
            if (_connectFromArea == null)
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
                {
                    _connectFromArea = GetTrainRailConnectAreaCollider();
                }
                if (_connectFromArea != null)
                {
                    var destination = _connectFromArea.CreateConnectionDestination();
                    var componentPosition = (Vector3Int)destination.railComponentID.Position;
                    Debug.Log($"接続スタート {destination.IsFront} {destination.railComponentID.ID} {destination.railComponentID.Position}");
                }
                return;
            }
            
            // 接続元のConnectionDestinationを算出
            // Compute ConnectionDestination for source endpoint
            var fromDestination = _connectFromArea.CreateConnectionDestination();
            if (fromDestination.IsDefault())
            {
                Debug.LogWarning("[TrainRailConnect] Invalid source destination detected.");
                _previewObject.SetActive(false);
                return;
            }
            
            // 接続先を取得（橋脚上ならConnectionDestination、そうでなければカーソル位置）
            // Get destination (ConnectionDestination if on pier, cursor position otherwise)
            var connectToArea = GetTrainRailConnectAreaCollider();
            if (connectToArea != null)
            {
                // 橋脚上にカーソルがある場合
                // Cursor is on a pier
                var toDestination = connectToArea.CreateConnectionDestination();
                if (toDestination.IsDefault())
                {
                    Debug.LogWarning("[TrainRailConnect] Invalid destination detected.");
                    _previewObject.SetActive(false);
                    return;
                }
                
                var previewData = CalculatePreviewData(fromDestination, toDestination, _cache);
                ShowPreview(previewData);
                SendProtocol(fromDestination, toDestination);
            }
            else
            {
                // カーソルが橋脚上にない場合、カーソル位置に向かってプレビュー
                // Cursor is not on a pier, show preview towards cursor position
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var cursorWorldPos, out _))
                {
                    _previewObject.SetActive(false);
                    return;
                }
                
                var previewData = CalculatePreviewData(fromDestination, cursorWorldPos, _cache);
                ShowPreview(previewData);
            }
            
            #region Internal
            
            void ShowPreview(TrainRailConnectPreviewData data)
            {
                _previewObject.SetActive(true);
                _previewObject.ShowPreview(data);
            }
            
            void SendProtocol(ConnectionDestination from, ConnectionDestination to)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                
                _previewObject.SetActive(false);

                if (!TryResolveNode(from, out var fromNodeId, out var fromGuid) ||
                    !TryResolveNode(to, out var toNodeId, out var toGuid))
                {
                    Debug.LogWarning("[TrainRailConnect] Failed to resolve node info from cache.");
                    _connectFromArea = null;
                    return;
                }
                Debug.Log($"Connecting rails: From NodeId={fromNodeId}, Guid={fromGuid} To NodeId={toNodeId}, Guid={toGuid}");
                ClientContext.VanillaApi.SendOnly.ConnectRail(fromNodeId, fromGuid, toNodeId, toGuid);
                _connectFromArea = null;
            }
            
            IRailComponentConnectAreaCollider GetTrainRailConnectAreaCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IRailComponentConnectAreaCollider>(_mainCamera, out var connectArea, Without_Player_MapObject_BlockBoundingBox_LayerMask);
                return connectArea;
            }

            bool TryResolveNode(ConnectionDestination destination, out int nodeId, out Guid nodeGuid)
            {
                nodeGuid = Guid.Empty;
                if (!_cache.TryGetNodeId(destination, out nodeId)) 
                    return false;
                if (!_cache.TryGetNode(nodeId, out var irailnode)) 
                    return false;
                nodeGuid = irailnode.NodeGuid;
                return true;
            }
            
            #endregion
        }
        public void Disable()
        {
        }
    }
}
