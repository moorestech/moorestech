using MainGame.Network.Interface.Send;
using UnityEngine;
using VContainer;

namespace MainGame.Control.Game
{
    public class BlockClickDetect : MonoBehaviour,IBlockClickDetect
    {
        private Camera _mainCamera;
        private MoorestechInputSettings _input;
        private IRequestBlockInventoryProtocol _requestBlockInventoryProtocol;
        
        [Inject]
        public void Construct(Camera mainCamera,IRequestBlockInventoryProtocol requestBlockInventoryProtocol)
        {
            _requestBlockInventoryProtocol = requestBlockInventoryProtocol;
            _mainCamera = mainCamera;
            _input = new MoorestechInputSettings();
            _input.Enable();
        }

        public void OnInput()
        {
        }

        public void OffInput()
        {
            
        }
        
        public bool IsBlockClicked()
        {
            
            
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            
            // マウスでクリックした位置が地面なら
            if (!Physics.Raycast(ray, out var hit)) return false;
            //TODO tagをやめる
            if (!hit.transform.gameObject.CompareTag("Block"))return false;
            
            
            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);
            
            //その位置のブロックインベントリを取得する
            _requestBlockInventoryProtocol.Send(x,y);
            
            
            return true;
        }
    }
}