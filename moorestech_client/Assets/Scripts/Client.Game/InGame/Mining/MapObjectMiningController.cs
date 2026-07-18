using Client.Common;
using Client.Game.InGame.Control.ViewMode;
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
        [SerializeField] private float miningDistance = 1.5f;
        
        private IMapObjectMiningState _currentState;
        private MapObjectMiningControllerContext _context;

        public string GetFocusTargetName()
        {
            return _context?.CurrentFocusMapObjectGameObject?.MapObjectMasterElement?.MapObjectName ?? "";
        }

        public bool IsMining() => _currentState is MapObjectMiningMiningState;

        public float GetMiningProgress()
        {
            return _currentState is MapObjectMiningMiningState mining ? mining.GetProgress() : 0;
        }
        
        [Inject]
        public void Constructor(ILocalPlayerInventory localPlayerInventory, HotBarView hotBarView)
        {
            _currentState = new MapObjectMiningIdleState();
            _context = new MapObjectMiningControllerContext(hotBarView, localPlayerInventory);
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

                var ray = Camera.main.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
                if (!Physics.Raycast(ray, out var hit, 10, LayerConst.MapObjectOnlyLayerMask)) return null;
                if (EventSystem.current.IsPointerOverGameObject()) return null;
                if (!hit.collider.gameObject.TryGetComponent(out MapObjectRayTarget mapObjectRayTarget)) return null;

                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObjectPos = mapObjectRayTarget.MapObjectGameObject.GetPosition();
                if (miningDistance < Vector3.Distance(playerPos, mapObjectPos)) return null;

                return mapObjectRayTarget.MapObjectGameObject;
            }

            #endregion
        }
    }
}
