using Client.Game.InGame.UI.Inventory.Equipment;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘ステート群が共有する状態と照合を持つコンテキスト
    ///     Context holding the state and lookups shared by the mining states
    /// </summary>
    public class MiningControllerContext
    {
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
