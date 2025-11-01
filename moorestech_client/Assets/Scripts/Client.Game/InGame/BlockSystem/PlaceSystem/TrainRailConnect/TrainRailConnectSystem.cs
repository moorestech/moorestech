using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Input;
using UnityEngine;
using static Client.Common.LayerConst;
using static Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.TrainRailConnectPreviewCalculator;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class TrainRailConnectSystem : IPlaceSystem
    {
        private readonly RailConnectPreviewObject _previewObject;
        private readonly Camera _mainCamera;
        
        private IRailComponentConnectAreaCollider _connectFromArea;
        public TrainRailConnectSystem(Camera mainCamera, RailConnectPreviewObject previewObject)
        {
            _mainCamera = mainCamera;
            _previewObject = previewObject;
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
                    Debug.Log($"接続スタート {_connectFromArea.IsFront} {_connectFromArea.CreateRailComponentSpecifier().Position.Vector3Int}");
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
            
            var previewData = CalculatePreviewData(_connectFromArea, connectToArea);
            ShowPreview();
            SendProtocol();
            
            #region Internal
            
            void ShowPreview()
            {
                _previewObject.SetActive(true);
                _previewObject.ShowPreview(previewData);
            }
            
            void SendProtocol()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                
                _previewObject.SetActive(false);
                
                var from = _connectFromArea.CreateRailComponentSpecifier();
                var to = connectToArea.CreateRailComponentSpecifier();
                
                Debug.Log($"接続 From:{_connectFromArea.IsFront} {from.Position.Vector3Int} To:{connectToArea.IsFront} {to.Position.Vector3Int}");
                
                ClientContext.VanillaApi.SendOnly.ConnectRail(from, to, previewData.IsFromFront, previewData.IsToFront);
                _connectFromArea = null;
                
            }
            
            IRailComponentConnectAreaCollider GetTrainRailConnectAreaCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IRailComponentConnectAreaCollider>(_mainCamera, out var connectArea, Without_Player_MapObject_BlockBoundingBox_LayerMask);
                return connectArea;
            }
            
            #endregion
        }
        public void Disable()
        {
        }
    }
}