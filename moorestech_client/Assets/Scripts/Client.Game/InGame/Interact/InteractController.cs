using Client.Game.InGame.Mining;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     GameScreenStateから毎フレーム駆動される司令塔。選定・ハイライト・単押し/長押しの振り分けを一箇所で行う
    ///     Driven every frame by GameScreenState: selection, highlight and tap/hold dispatch in one place
    /// </summary>
    public class InteractController
    {
        private readonly MiningControllerContext _miningContext;
        private readonly InteractTargetSelector _selector;
        private readonly TapInteractionDriver _tapDriver = new();

        private IInteractable _highlighted;
        private IMiningState _miningState = new MiningIdleState();

        public InteractController(LocalPlayerEquipment localPlayerEquipment, InteractTargetSelector selector)
        {
            _selector = selector;
            _miningContext = new MiningControllerContext(localPlayerEquipment);
        }

        public UITransitContext ManualUpdate()
        {
            var target = _selector.Select();
            ApplyHighlight(target);

            // 長押し系は採掘FSMがそのまま担う（対象でなければnullが渡りIdleへ戻る）
            // Hold interactions stay with the mining FSM; a non-mining target passes null and it idles
            _miningContext.SetFocusTarget(target as IMiningTargetObject);
            _miningState = _miningState.GetNextUpdate(_miningContext, Time.deltaTime);

            if (target is ITapInteractable tapTarget) return _tapDriver.Step(tapTarget);

            _tapDriver.Clear();
            return null;
        }

        public void Disable()
        {
            ApplyHighlight(null);
            _tapDriver.Clear();
            _miningContext.SetFocusTarget(null);
            _miningState = new MiningIdleState();
        }

        private void ApplyHighlight(IInteractable target)
        {
            if (ReferenceEquals(_highlighted, target)) return;

            // 別実体でも同じGameObjectを指すなら見た目は変わらない
            // Different instances pointing at one GameObject show the same outline, so nothing toggles
            var isSameObject = _highlighted != null && target != null && _highlighted.GameObject == target.GameObject;
            if (!isSameObject)
            {
                _highlighted?.SetHighlighted(false);
                target?.SetHighlighted(true);
            }

            _highlighted = target;
        }
    }
}
