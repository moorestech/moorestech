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
        private IReadOnlyList<string> _hintParams;

        public InputKey Key => InputManager.Playable.Interact;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractOpenBlock;
        public IReadOnlyList<string> HintParams => _hintParams;

        public BlockOpenInteractAction(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            RefreshHintParams();
        }

        // 言語切替後にヒントのブロック名を再解決する（呼び出しはBlockInteractableのOnLanguageChanged購読）
        // Re-resolve the hint's block name after a language change (invoked from BlockInteractable's OnLanguageChanged subscription)
        internal void RefreshHintParams()
        {
            _hintParams = new[] { Localize.GetContent(ContentLocalizationKeys.BlockName(_blockGameObject.BlockMasterElement.BlockGuid)) };
        }

        public UITransitContext Execute()
        {
            var container = UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(_blockGameObject));
            return new UITransitContext(UIStateEnum.SubInventory, container);
        }
    }
}
