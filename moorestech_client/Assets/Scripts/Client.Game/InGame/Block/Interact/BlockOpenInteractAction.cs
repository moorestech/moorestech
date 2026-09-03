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
    internal class BlockOpenInteractAction : ITapInteractAction
    {
        private readonly BlockGameObject _blockGameObject;

        public InputKey Key => InputManager.Playable.Interact;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractOpenBlock;

        // 読まれるのは対象が変わった瞬間だけなので、購読で保持せずその場で解決する（言語切替も自動で乗る）
        // Read only when the target changes, so it resolves on the spot instead of caching through a subscription, which also picks up a language switch
        public IReadOnlyList<string> HintParams => new[] { Localize.GetContent(ContentLocalizationKeys.BlockName(_blockGameObject.BlockMasterElement.BlockGuid)) };

        internal BlockOpenInteractAction(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
        }

        public InteractExecuteResult Execute()
        {
            var container = UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(_blockGameObject));
            return InteractExecuteResult.Transit(new UITransitContext(UIStateEnum.SubInventory, container));
        }
    }
}
