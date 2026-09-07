using System;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Mining
{
    public class MiningFocusState : IMiningState
    {
        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            // フォーカスが外れたのであればIdleに遷移
            // If the focus is lost, transition to Idle
            var currentTarget = context.CurrentFocusTarget;
            if (currentTarget == null) return new MiningIdleState(context);

            // 装備を渡して可否・種別・ツールを一度に問い合わせる
            // Ask once for availability, kind and tool with the equipment applied
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;
            var outcome = currentTarget.TryBeginHandMining(equippedItemId, out var usableMiningTool);
            var earnItemNames = context.CurrentFocusTargetEarnItemNames;

            switch (outcome)
            {
                case MiningStartOutcome.Unavailable:
                    return new MiningIdleState(context);
                case MiningStartOutcome.InstantPickUp:
                    return PickUpProcess(context);
                case MiningStartOutcome.HandMiningNotAllowed:
                    // 掘れない理由を出して維持する
                    // Show why it cannot be mined and keep focus
                    ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedCannotHandMine, LocalizationKeys.Ui.Tooltip.CannotHandMine, Array.Empty<string>());
                    return this;
                case MiningStartOutcome.ToolMismatch:
                    // 無効装備ならフォーカス維持。必要アイテム名はフォーカス時に組み立て済み（ADR 0033）
                    // Keep focus for invalid equipment; the required item names were assembled when focus was taken (ADR 0033)
                    ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedRequiredItems, LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { context.CurrentFocusTargetRecommendedToolNames });
                    return this;
            }

            // Fが押されていない場合はフォーカスを維持
            // Keep focus while F is not held
            if (!InputManager.Playable.Interact.GetKey)
            {
                ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedMineHold, LocalizationKeys.Ui.Tooltip.HoldToGet, Array.Empty<string>());
                return this;
            }

            // マイニング状態に遷移
            // Transition to mining state
            context.Tooltip.Hide(MiningControllerContext.TooltipOwner);
            return new MiningProgressState(context, currentTarget, usableMiningTool);

            #region Internal

            IMiningState PickUpProcess(MiningControllerContext pickUpContext)
            {
                if (InputManager.Playable.Interact.GetKeyDown)
                {
                    pickUpContext.Tooltip.Hide(MiningControllerContext.TooltipOwner);
                    return new MiningCompleteState(pickUpContext.CurrentFocusTarget);
                }

                // Fが押されていなければ現状を維持
                // Keep the current state while F is not pressed
                ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedMineClick, LocalizationKeys.Ui.Tooltip.PickUpInteract, Array.Empty<string>());
                return this;
            }

            // 取得物名が無ければ従来キーへ戻し、取得物名は先頭パラメータとして差し込む（ADR 0033）
            // Fall back to the nameless key when the target yields nothing, otherwise the earned name leads the params (ADR 0033)
            void ShowEarnItemNamed(LocalizationKey namedKey, LocalizationKey unnamedKey, string[] tailParams)
            {
                if (earnItemNames.Length == 0)
                {
                    context.Tooltip.Show(MiningControllerContext.TooltipOwner, unnamedKey, tailParams);
                    return;
                }

                var namedParams = new string[tailParams.Length + 1];
                namedParams[0] = earnItemNames;
                tailParams.CopyTo(namedParams, 1);
                context.Tooltip.Show(MiningControllerContext.TooltipOwner, namedKey, namedParams);
            }

            #endregion
        }
    }
}
