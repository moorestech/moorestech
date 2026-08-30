using Client.Game.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.Block.Interact
{
    /// <summary>
    ///     開けるブロックにだけインタラクト面を付与する。BlockGameObjectPrefabContainerと同じ条件をテストから直接検証できるよう分離
    ///     Attach the interact face only to openable blocks; split out so the attach condition can be tested directly, matching BlockGameObjectPrefabContainer's usage
    /// </summary>
    public static class BlockInteractableAttacher
    {
        public static void AttachIfOpenable(GameObject blockObject, BlockMasterElement blockMasterElement)
        {
            if (blockMasterElement.IsBlockOpenable()) blockObject.AddComponent<BlockInteractable>();
        }
    }
}
