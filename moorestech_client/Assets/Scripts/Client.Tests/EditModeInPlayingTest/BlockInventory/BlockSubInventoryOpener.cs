using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Interact;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    ///     サブインベントリを実プレイと同じ文脈で開く
    ///     Opens a block's sub inventory with the same transit context real play uses
    /// </summary>
    public static class BlockSubInventoryOpener
    {
        public static async UniTask<SubInventoryState> Open(BlockGameObject blockGameObject)
        {
            var subInventoryState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<SubInventoryState>();

            // OnEnterはCurrentSubInventoryをクリアしないため、前回のOpen()の残留を先に払っておく
            // OnEnter does not clear CurrentSubInventory, so discard any leftover from a previous Open() first
            subInventoryState.OnExit();

            // 遷移文脈は本番のインタラクトに組ませる。テスト側でコンテナを組み直すと配線変更に追随しない
            // Let the production interact build the transit context; rebuilding it here would not follow wiring changes
            var interactable = blockGameObject.GetComponent<BlockInteractable>();
            Assert.IsNotNull(interactable, "block has no BlockInteractable");
            Assert.AreEqual(1, interactable.Actions.Count, "unexpected interact action count");

            var result = interactable.Actions[0].Execute();
            Assert.IsTrue(result.IsHandled, "interact action did not handle the tap");
            Assert.IsNotNull(result.TransitContext, "interact action requested no UI transition");

            subInventoryState.OnEnter(result.TransitContext);

            // サーバーからのインベントリ取得完了を待つ
            // Wait until the inventory fetch from the server completes
            for (var i = 0; i < 300 && subInventoryState.CurrentSubInventory == null; i++) await UniTask.Yield();
            Assert.IsNotNull(subInventoryState.CurrentSubInventory, "sub inventory did not load");

            return subInventoryState;
        }

        public static void Close(SubInventoryState subInventoryState)
        {
            subInventoryState.OnExit();
        }
    }
}
