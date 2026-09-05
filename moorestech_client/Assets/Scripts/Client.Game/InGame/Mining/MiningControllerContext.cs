using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘ステート群が共有する状態と照合を持つコンテキスト
    ///     Context holding the state and lookups shared by the mining states
    /// </summary>
    public class MiningControllerContext
    {
        // 採掘ステートは遷移ごとに作り直されるため、系統で1つの所有トークンを共有する
        // Mining states are recreated on every transition, so the whole flow shares one owner token
        public static readonly TooltipOwner TooltipOwner = new();

        public IMiningTargetObject CurrentFocusTarget { get; private set; }

        // フォーカス変化時だけ組み立てる
        // Built only when focus changes so no per-frame re-resolution happens
        public string CurrentFocusTargetEarnItemNames { get; private set; } = string.Empty;

        // 推奨ツール名も取得物名と同じ契機でのみ組み立てる
        // The recommended tool names are built on the very same trigger as the earned item names
        public string CurrentFocusTargetRecommendedToolNames { get; private set; } = string.Empty;

        public readonly LocalPlayerEquipment LocalPlayerEquipment;
        public ProgressBarState ProgressBar { get; }

        public MiningControllerContext(LocalPlayerEquipment localPlayerEquipment, ProgressBarState progressBar)
        {
            LocalPlayerEquipment = localPlayerEquipment;
            ProgressBar = progressBar;

            // 言語切替で保持中の名前が古くなる
            // A language switch makes the cached names stale
            Localize.OnLanguageChanged.Subscribe(_ => ResolveFocusTargetNames());
        }

        public void SetFocusTarget(IMiningTargetObject target)
        {
            // 同一対象なら再解決も要らない
            // The same target needs no re-resolution
            if (ReferenceEquals(CurrentFocusTarget, target)) return;

            CurrentFocusTarget = target;
            ResolveFocusTargetNames();
        }

        // 表示名はここでしか組み立てない。フォーカス中のフレームでは一切解決し直さない
        // Display names are assembled only here; not a single frame of focus re-resolves them
        private void ResolveFocusTargetNames()
        {
            CurrentFocusTargetEarnItemNames = JoinItemNames(CurrentFocusTarget?.EarnItemGuids);
            CurrentFocusTargetRecommendedToolNames = JoinToolNames(CurrentFocusTarget?.RecommendedToolItemIds);

            #region Internal

            // 取得物を持たない対象は名前欄を空にする
            // A target that yields nothing leaves the name slot empty
            string JoinItemNames(IReadOnlyList<Guid> itemGuids)
            {
                if (itemGuids == null || itemGuids.Count == 0) return string.Empty;

                var itemNames = new string[itemGuids.Count];
                for (var index = 0; index < itemGuids.Count; index++)
                {
                    itemNames[index] = Localize.GetContent(ContentLocalizationKeys.ItemName(itemGuids[index]));
                }

                return string.Join(", ", itemNames);
            }

            // ツールを要求しない対象も同様に空欄にする
            // A target requiring no tool leaves the slot empty as well
            string JoinToolNames(IReadOnlyList<ItemId> toolItemIds)
            {
                if (toolItemIds == null || toolItemIds.Count == 0) return string.Empty;

                var toolGuids = new Guid[toolItemIds.Count];
                for (var index = 0; index < toolItemIds.Count; index++)
                {
                    toolGuids[index] = MasterHolder.ItemMaster.GetItemMaster(toolItemIds[index]).ItemGuid;
                }

                return JoinItemNames(toolGuids);
            }

            #endregion
        }
    }
}
