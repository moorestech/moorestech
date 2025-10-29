using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class TrainRailConnectSystem : IPlaceSystem
    {
        private Camera _mainCamera;
        private BlockGameObject _connectFromBlock;
        
        public void Enable()
        {
            _connectFromBlock = null;
        }
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            if (_connectFromBlock == null)
            {
                GetFromBlock();
            }
            
            ShowPreview();
            
            #region Internal
            
            void GetFromBlock()
            {
                var clickedBlock = GetClickedBlock();
                if (clickedBlock == null) return;
                _connectFromBlock = clickedBlock;
            }
            
            void ShowPreview()
            {
                
            }
            
            void GetToBlock()
            {
                var clickedBlock = GetClickedBlock();
                if (clickedBlock == null) return;

                // TODO 接続処理
            }
            
            BlockGameObject GetClickedBlock()
            {
                if (!InputManager.Playable.ScreenRightClick.GetKeyDown) return null;
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var pos, out var surface)) return null;
                
                return surface.BlockGameObject;
            }
            
            #endregion
        }
        public void Disable()
        {
            throw new System.NotImplementedException();
        }
    }
}