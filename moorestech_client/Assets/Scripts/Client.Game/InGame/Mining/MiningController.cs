using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Equipment;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     照準下の採掘対象を解決し、採掘ステートを駆動する
    ///     Resolves the mining target under the aim point and drives the mining states
    /// </summary>
    public class MiningController : MonoBehaviour
    {
        [SerializeField] private float miningDistance = 1.5f;

        private IMiningState _currentState;
        private MiningControllerContext _context;

        [Inject]
        public void Constructor(LocalPlayerEquipment localPlayerEquipment)
        {
            _currentState = new MiningIdleState();
            _context = new MiningControllerContext(localPlayerEquipment);
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

                // 対象種別を問わず共通マーカーから解決する
                // Resolve from the shared marker regardless of the target kind
                if (!hit.collider.gameObject.TryGetComponent(out IMiningRayTarget rayTarget)) return null;
                var target = rayTarget.MiningTargetObject;
                if (target == null) return null;

                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                if (miningDistance < Vector3.Distance(playerPos, target.GameObject.transform.position)) return null;

                return target;
            }

            #endregion
        }
    }
}
