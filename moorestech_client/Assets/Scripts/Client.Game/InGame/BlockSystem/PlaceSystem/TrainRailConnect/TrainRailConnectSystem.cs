using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
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
        private readonly ILocalPlayerInventory _playerInventory;
        
        private IRailComponentConnectAreaCollider _connectFromArea;
        public TrainRailConnectSystem(Camera mainCamera, RailConnectPreviewObject previewObject, RailGraphClientCache cache, LocalPlayerInventoryController localPlayerInventory)
        {
            _mainCamera = mainCamera;
            _previewObject = previewObject;
            _cache = cache;
            _playerInventory = localPlayerInventory.LocalPlayerInventory;
        }
        
        public void Enable()
        {
            _connectFromArea = null;
        }
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
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
                    var componentPosition = destination.blockPosition;
                    Debug.Log($"[TrainRailConnect] Select FROM: IsFront={_connectFromArea.IsFront} pos=({componentPosition.x},{componentPosition.y},{componentPosition.z})");
                }
                return;
            }
            
            // Compute ConnectionDestination for both endpoints
            var fromDestination = _connectFromArea.CreateConnectionDestination();
            
            // If the connection point is not under the cursor, return.
            var connectToArea = GetTrainRailConnectAreaCollider();
            if (connectToArea == null)
            {
                if (PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var position, out _))
                {
                    var previewData = CalculatePreviewData(fromDestination, position, _cache, _playerInventory);
                    ShowPreview(previewData);
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
                
                var previewData = CalculatePreviewData(fromDestination, toDestination, _cache, _playerInventory);
                ShowPreview(previewData);
                
                if (!previewData.HasEnoughRailItem) return;
                
                SendProtocol(fromNode, toNode, previewData.RailTypeGuid);   
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
            }
            
            void SendProtocol(IRailNode from, IRailNode to, Guid railTypeGuid)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                
                _previewObject.SetActive(false);
                
                Debug.Log($"Connecting rails: From NodeId={from.NodeId}, Guid={from.NodeGuid} To NodeId={to.NodeId}, Guid={to.NodeGuid}");
                ClientContext.VanillaApi.SendOnly.ConnectRail(from.NodeId, from.NodeGuid, to.NodeId, to.NodeGuid, railTypeGuid);
                _connectFromArea = null;
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
        public void Disable()
        {
            _previewObject.SetActive(false);
        }
    }
}
