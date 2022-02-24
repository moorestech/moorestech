using MainGame.Control.UI.Inventory;
using MainGame.Network.Send;
using MainGame.UnityView.Chunk;
using UnityEngine;
using VContainer;

namespace MainGame.Control.Game.MouseKeyboard
{
    public class BlockClickDetect : MonoBehaviour,IBlockClickDetect
    {
        private Camera _mainCamera;
        private MoorestechInputSettings _input;
        private RequestBlockInventoryProtocol _requestBlockInventoryProtocol;
        private BlockInventoryPlayerInventoryItemMoveService _blockInventoryPlayerInventoryItemMoveService;
        
        [Inject]
        public void Construct(Camera mainCamera,RequestBlockInventoryProtocol requestBlockInventoryProtocol,
            BlockInventoryPlayerInventoryItemMoveService blockInventoryPlayerInventoryItemMoveService)
        {
            _blockInventoryPlayerInventoryItemMoveService = blockInventoryPlayerInventoryItemMoveService;
            _requestBlockInventoryProtocol = requestBlockInventoryProtocol;
            _mainCamera = mainCamera;
            _input = new MoorestechInputSettings();
            _input.Enable();
        }
        
        public bool IsBlockClicked()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // マウスでクリックした位置が地面なら
            if (!Physics.Raycast(ray, out var hit)) return false;
            if (hit.collider.gameObject.GetComponent<BlockGameObject>() == null) return false;

            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);
            
            //その位置のブロックインベントリを取得するパケットを送信する
            //実際にインベントリのパケットを取得できてからUIを開くため、実際の開く処理はNetworkアセンブリで行う
            //ここで呼び出す処理が多くなった場合イベントを使うことを検討する
            _requestBlockInventoryProtocol.Send(x,y);
            _blockInventoryPlayerInventoryItemMoveService.SetBlockPosition(x,y);
                
            
            return true;
        }
    }
}