using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Equipment;
using UnityEngine;
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
        
        [Inject]
        public void Constructor(LocalPlayerEquipment localPlayerEquipment)
        {
            _currentState = new MapObjectMiningIdleState();
            _context = new MapObjectMiningControllerContext(localPlayerEquipment);
        }
        
        
        private void Update()
        {
            // 照準下の採掘対象をコンテキストへ反映する
            // Apply the mining target under the aim point to the context
            var currentTarget = GetCurrentTarget();
            _context.SetFocusTarget(currentTarget);
            
            // update state
            _currentState = _currentState.GetNextUpdate(_context, Time.deltaTime);

            #region Internal

            IMiningTargetObject GetCurrentTarget()
            {
                if (Camera.main == null) return null;

                var ray = Camera.main.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
                if (!Physics.Raycast(ray, out var hit, 10, LayerConst.MapObjectOnlyLayerMask)) return null;
                if (UiPointerHitTest.IsPointerOverAnyUi()) return null;
                var target = ResolveMiningTarget(hit.collider.gameObject);
                if (target == null) return null;
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                if (miningDistance < Vector3.Distance(playerPos, target.GameObject.transform.position)) return null;

                return target;
            }

            IMiningTargetObject ResolveMiningTarget(GameObject hitObject)
            {
                // 既存mapObjectを優先し、該当しなければ露頭マーカーを解決する
                // Prefer an existing map object, then resolve an outcrop marker when absent
                if (hitObject.TryGetComponent(out MapObjectRayTarget mapObjectRayTarget))
                    return mapObjectRayTarget.MapObjectGameObject;
                if (hitObject.TryGetComponent(out OutcropRayTarget outcropRayTarget))
                    return outcropRayTarget.OutcropGameObject;
                return null;
            }

            #endregion
        }
    }
}
