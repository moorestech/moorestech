using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Tooltip;

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

        public readonly LocalPlayerEquipment LocalPlayerEquipment;

        public MiningControllerContext(LocalPlayerEquipment localPlayerEquipment)
        {
            LocalPlayerEquipment = localPlayerEquipment;
        }

        public void SetFocusTarget(IMiningTargetObject target)
        {
            var currentGameObject = CurrentFocusTarget?.GameObject;
            var nextGameObject = target?.GameObject;

            // 実体変更時だけ通知
            // Notify only on concrete change
            if (currentGameObject != nextGameObject)
            {
                CurrentFocusTarget?.SetFocused(false);
                target?.SetFocused(true);
            }

            CurrentFocusTarget = target;
        }
    }
}
