using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Input;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.TrainRailConnectPreviewCalculator;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class TrainRailConnectSystem : IPlaceSystem
    {
        private readonly RailConnectPreviewObject _previewObject;
        private readonly Camera _mainCamera;
        
        private BlockGameObject _connectFromBlock;
        public TrainRailConnectSystem(Camera mainCamera, RailConnectPreviewObject previewObject)
        {
            _mainCamera = mainCamera;
            _previewObject = previewObject;
        }
        
        public void Enable()
        {
            _connectFromBlock = null;
        }
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            if (_connectFromBlock == null)
            {
                _connectFromBlock = GetFromBlock();
                return;
            }
            
            var connectToArea = GetToTrainRailConnectAreaCollider();
            var previewData = CalculatePreviewData(_connectFromBlock, connectToArea);
            
            ShowPreview();
            SendProtocol();
            
            #region Internal
            
            BlockGameObject GetFromBlock()
            {
                if (!InputManager.Playable.ScreenRightClick.GetKeyDown) return null;
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out _, out var surface)) return null;
                
                return surface.BlockGameObject;
            }
            
            void ShowPreview()
            {
                if (connectToArea == null)
                {
                    _previewObject.SetActive(false);
                    return;
                }
                
                _previewObject.SetActive(true);
                _previewObject.ShowPreview(previewData);
            }
            
            void SendProtocol()
            {
                if(InputManager.Playable.ScreenLeftClick.GetKeyDown)
                {
                    _previewObject.SetActive(false);
                    _connectFromBlock = null;
                    
                    // TODO プロトコル送信処理
                }
            }
            
            TrainRailConnectAreaCollider GetToTrainRailConnectAreaCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<TrainRailConnectAreaCollider>(_mainCamera, out var connectArea);
                return connectArea;
            }
            
            #endregion
        }
        public void Disable()
        {
        }
    }
}