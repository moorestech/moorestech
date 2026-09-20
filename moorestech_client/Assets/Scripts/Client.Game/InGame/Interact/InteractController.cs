using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.Mining;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     毎フレームの司令塔。選定/ハイライト/タップ長押しを一元化
    ///     Driven every frame: selection, highlight and tap/hold dispatch in one place
    /// </summary>
    public class InteractController
    {
        private readonly MiningControllerContext _miningContext;
        private readonly IInteractTargetSelector _selector;
        private readonly TapInteractionDriver _tapDriver;

        // 採掘FSMが長押しで使うキー（ADR 0065）
        // The key the mining FSM holds (ADR 0065)
        private readonly IReadOnlyList<InputKey> _miningHoldKeys;

        private IInteractable _highlighted;
        private GameObject _highlightedGameObject;
        private IMiningState _miningState;

        public InteractController(LocalPlayerEquipment localPlayerEquipment, IInteractTargetSelector selector, ProgressBarState progressBar, IMouseCursorTooltip tooltip)
        {
            _selector = selector;
            _miningContext = new MiningControllerContext(localPlayerEquipment, progressBar, tooltip);
            _tapDriver = new TapInteractionDriver(tooltip);
            _miningState = new MiningIdleState(_miningContext);
            _miningHoldKeys = new[] { InputManager.Playable.Interact };
        }

        public InteractExecuteResult ManualUpdate()
        {
            var selection = _selector.Scan();
            var target = selection.Primary;
            ApplyHighlight(target);

            // 長押しは採掘FSMが担う。対象外はnullでIdleへ
            // Hold interactions stay with the mining FSM; a non-mining target passes null and it idles
            _miningContext.SetFocusTarget(target as IMiningTargetObject);
            _miningState = _miningState.GetNextUpdate(_miningContext, Time.deltaTime);

            // 採掘対象が主対象ならFは近傍へ回さない
            // While a mining target is primary, F is never forwarded to nearby candidates
            var primaryHoldKeys = target is IMiningTargetObject ? _miningHoldKeys : Array.Empty<InputKey>();
            return _tapDriver.Step(target as ITapInteractable, selection, primaryHoldKeys);
        }

        public void Disable()
        {
            ApplyHighlight(null);
            _tapDriver.Clear();

            // 採掘中のステートを捨てると進捗バーとアニメが戻らないため、フォーカスを外してIdleまでFSMを正規遷移させる
            // Discarding a live mining state would strand the progress bar and animation, so drop focus and run the FSM down to Idle
            _miningContext.SetFocusTarget(null);
            while (_miningState is not MiningIdleState) _miningState = _miningState.GetNextUpdate(_miningContext, 0f);
        }

        private void ApplyHighlight(IInteractable target)
        {
            // 破棄済みGameObjectはUnityのfake-null判定を通してから墓標を捨てる（interface型のまま比較すると偽陽性でMissingReferenceException）
            // Discard a destroyed target's tombstone through Unity's fake-null check first; comparing via the interface type alone false-positives into MissingReferenceException
            if (_highlightedGameObject == null) _highlighted = null;

            if (ReferenceEquals(_highlighted, target)) return;

            _highlighted?.SetHighlighted(false);
            target?.SetHighlighted(true);

            _highlighted = target;
            _highlightedGameObject = target?.GameObject;
        }
    }
}
