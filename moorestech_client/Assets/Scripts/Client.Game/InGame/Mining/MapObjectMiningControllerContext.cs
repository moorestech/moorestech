using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Inventory.Equipment;
using Core.Master;
using Mooresmaster.Model.MapModule;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘ステート群が共有する状態と照合を持つコンテキスト
    ///     Context holding the state and lookups shared by the mining states
    /// </summary>
    public class MapObjectMiningControllerContext
    {
        public MapObjectGameObject CurrentFocusMapObjectGameObject { get; private set; }

        public readonly LocalPlayerEquipment LocalPlayerEquipment;

        public MapObjectMiningControllerContext(LocalPlayerEquipment localPlayerEquipment)
        {
            LocalPlayerEquipment = localPlayerEquipment;
        }

        /// <summary>
        ///     装備中アイテムに対応する採掘ツールを引く。ステート間で依存させないためコンテキストが持つ
        ///     Resolves the mining tool matching the equipped item; the context owns it so states never depend on each other
        /// </summary>
        public MiningToolsElement ResolveUsableTool(MiningToolsElement[] miningTools)
        {
            var equippedItemId = LocalPlayerEquipment.SelectedItem.Id;
            if (equippedItemId == ItemMaster.EmptyItemId) return null;

            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var miningTool in miningTools)
            {
                if (miningTool.ToolItemGuid == equippedItemGuid) return miningTool;
            }

            return null;
        }

        public void SetFocusMapObjectGameObject(MapObjectGameObject mapObjectGameObject)
        {
            if (mapObjectGameObject != CurrentFocusMapObjectGameObject)
            {
                CurrentFocusMapObjectGameObject?.OnFocus(false);
                mapObjectGameObject?.OnFocus(true);
            }

            CurrentFocusMapObjectGameObject = mapObjectGameObject;
        }
    }
}
