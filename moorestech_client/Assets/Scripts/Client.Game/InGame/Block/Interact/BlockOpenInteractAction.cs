using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Block.Interact
{
    /// <summary>
    ///     Fで機械UIを開く
    ///     Opens the machine UI with F
    /// </summary>
    public class BlockOpenInteractAction : ITapInteractAction
    {
        private readonly BlockGameObject _blockGameObject;

        public InputKey Key => InputManager.Playable.Interact;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractOpenBlock;

        // ブロック名は言語切替で変わるので、生成時に固めず参照のたびに引く
        // The block name changes with the language, so it is resolved on every read instead of being frozen at construction
        public IReadOnlyList<string> HintParams => new[] { Localize.GetContent(ContentLocalizationKeys.BlockName(_blockGameObject.BlockMasterElement.BlockGuid)) };

        public BlockOpenInteractAction(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
        }

        public UITransitContext Execute()
        {
            var container = UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(_blockGameObject));
            return new UITransitContext(UIStateEnum.SubInventory, container);
        }
    }
}
