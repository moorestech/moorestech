using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectMiningController : MonoBehaviour
    {
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private float miningDistance = 1.5f;
        
        private IMapObjectMiningState _currentState;
        private MapObjectMiningControllerContext _context;
        
        [Inject]
        public void Constructor(ILocalPlayerInventory localPlayerInventory, IPlayerObjectController playerObjectController)
        {
            _currentState = new MapObjectMiningIdleState();
            _context = new MapObjectMiningControllerContext(hotBarView, localPlayerInventory, playerObjectController);
        }
        
        
        private void Update()
        {
            // update focus map object
            var currentMapObject = GetCurrentMapObject();
            _context.SetFocusMapObjectGameObject(currentMapObject);
            
            // update state
            _currentState = _currentState.GetNextUpdate(_context, Time.deltaTime);
            
            #region Internal
            
            MapObjectGameObject GetCurrentMapObject()
            {
                if (Camera.main == null) return null;
                
                var ray = Camera.main.ScreenPointToRay(new Vector2(Screen.width / 2.0f, Screen.height / 2.0f));
                if (!Physics.Raycast(ray, out var hit, 10, LayerConst.MapObjectOnlyLayerMask)) return null;
                if (EventSystem.current.IsPointerOverGameObject()) return null;
                if (!hit.collider.gameObject.TryGetComponent(out MapObjectGameObject mapObject)) return null;
                
                var playerPos = _context.PlayerObjectController.Position;
                var mapObjectPos = mapObject.transform.position;
                if (miningDistance < Vector3.Distance(playerPos, mapObjectPos)) return null;
                
                return mapObject;
            }
            
            #endregion
        }
    }
}