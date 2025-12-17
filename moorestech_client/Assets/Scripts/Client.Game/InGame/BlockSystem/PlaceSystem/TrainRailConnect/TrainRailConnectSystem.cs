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
                    Debug.Log($"接続スタート {_connectFromArea.IsFront} {componentPosition}");
                }
                return;
            }
            
            // 接続先がカーソル上になければreturn
            // If the connection point is not under the cursor, return.
            var connectToArea = GetTrainRailConnectAreaCollider();
            if (connectToArea == null)
            {
                _previewObject.SetActive(false);
                return;
            }
            
            // 接続対象のConnectionDestinationを算出
            // Compute ConnectionDestination for both endpoints
            var fromDestination = _connectFromArea.CreateConnectionDestination();
            var toDestination = connectToArea.CreateConnectionDestination();
            if (fromDestination.IsDefault() || toDestination.IsDefault())
            {
                Debug.LogWarning("[TrainRailConnect] Invalid destination detected. Re-select connection target.");
                _previewObject.SetActive(false);
                return;
            }
            
            var previewData = CalculatePreviewData(_connectFromArea, connectToArea);
            ShowPreview();
            SendProtocol(fromDestination, toDestination);
            
            #region Internal
            
            void ShowPreview()
            {
                _previewObject.SetActive(true);
                _previewObject.ShowPreview(previewData);
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
