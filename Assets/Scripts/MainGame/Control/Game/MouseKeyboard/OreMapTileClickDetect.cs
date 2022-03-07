using System;
using MainGame.UnityView.WorldMapTile;
using UnityEngine;
using VContainer;

namespace MainGame.Control.Game.MouseKeyboard
{
    public class OreMapTileClickDetect : MonoBehaviour
    {
        private Camera _mainCamera;
        private MoorestechInputSettings _input;
        
        [Inject]
        public void Construct(Camera mainCamera)
        {
            _mainCamera = mainCamera;
            _input = new MoorestechInputSettings();
            _input.Enable();
        }

        private void Update()
        {
            
        }

        private bool IsBlockClicked()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // マウスでクリックした位置にタイルマップがあるとき
            if (!_input.Playable.ScreenClick.triggered) return false;
            if (!Physics.Raycast(ray, out var hit)) return false;
            if (hit.collider.gameObject.GetComponent<OreTileObject>() == null) return false;
            
            return true;
        }
    }
}