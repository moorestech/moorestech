using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
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

        public readonly LocalPlayerEquipment LocalPlayerEquipment;

        public MiningControllerContext(LocalPlayerEquipment localPlayerEquipment)
        {
            LocalPlayerEquipment = localPlayerEquipment;

            // 言語切替で保持中の名前が古くなる
            // A language switch makes the cached names stale
            Localize.OnLanguageChanged.Subscribe(_ => ResolveEarnItemNames());
        }

        public void SetFocusTarget(IMiningTargetObject target)
        {
            // 同一対象なら再解決も要らない
            // The same target needs no re-resolution
            if (ReferenceEquals(CurrentFocusTarget, target)) return;

            CurrentFocusTarget = target;
            ResolveEarnItemNames();
        }

        private void ResolveEarnItemNames()
        {
            var earnItemGuids = CurrentFocusTarget?.EarnItemGuids;

            // 取得物を持たない対象は名前欄を空にする
            // A target that yields nothing leaves the name slot empty
            if (earnItemGuids == null || earnItemGuids.Count == 0)
            {
                CurrentFocusTargetEarnItemNames = string.Empty;
                return;
            }

            var itemNames = new string[earnItemGuids.Count];
            for (var index = 0; index < earnItemGuids.Count; index++)
            {
                itemNames[index] = Localize.GetContent(ContentLocalizationKeys.ItemName(earnItemGuids[index]));
            }

            CurrentFocusTargetEarnItemNames = string.Join(", ", itemNames);
        }
    }
}
