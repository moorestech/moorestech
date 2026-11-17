using System;
using System.Linq;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Inventory;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Recipe;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe
{
    /// <summary>
    /// レシピ画面の購読がインベントリ画面の間だけで、出すときは隠れたまま最新の値を受けてから出すことを本物の Model と Presenter で確かめる（§4.2 の ①。前例 BlockInventoryScreenSubscriptionTest）
    /// Verifies with the real model and presenter that the recipe screen follows only on the inventory screen and takes the latest value while hidden before showing (§4.2 type 1; precedent BlockInventoryScreenSubscriptionTest)
    /// </summary>
    public class RecipeScreenSubscriptionTest : ScreenTestHost
    {
        private GameObject _host;
        private UIStateControl _control;
        private RecipeItemSelection _selection;
        private RecipeScreenModel _model;
        private CountingItemTextureSource _textures;
        private StageColumnsLayout _columns;
        private Guid _recipeGuid;
        private ItemId _resultItemId;

        [SetUp]
        public void MountPresenter()
        {
            // 解放はテスト mod のサーバー状態。機械レシピの源は Initialize せず空のまま
            // Unlocks come from the test-mod server state; the machine-recipe source stays empty without Initialize
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            var recipe = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes().First(r => unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
            _recipeGuid = recipe.CraftRecipeGuid;
            _resultItemId = MasterHolder.ItemMaster.GetItemId(recipe.CraftResultItemGuid);

            // 本番の登録と同じく Model を先に Initialize し、ルータの通知で Model が Presenter より先に組み直す
            // As in production registration, initialize the model first so it rebuilds before the presenter on router notifications
            _host = new GameObject("recipe-subscription");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.PlayerInventory, new[] { UIStateEnum.GameScreen, UIStateEnum.PlayerInventory }, out _control);
            _selection = new RecipeItemSelection();
            var catalog = new RecipeViewerItemCatalog(new ItemRecipeViewerDataContainer(unlock));
            _model = new RecipeScreenModel(unlock, catalog, new UnlockedMachineRecipeSource(unlock, null, null), new CapturingVanillaApiEvent(), new RecordingPlayerInventoryOperations(36, 3), _selection, routing);
            _model.Initialize();

            _columns = new StageColumnsLayout();
            rootVisualElement.Add(_columns);
            _textures = new CountingItemTextureSource();
            new RecipeScreenPresenter(_columns, routing, _model, new GameContentPresentation(_textures), new RecordingRecipeCommands(), _selection).Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void 出たら最新の値で1回描く()
        {
            // 未選択で描いてから隠し、隠れている間に選んだアイテムが戻った直後に描かれる
            // Drawn with no selection then hidden; the item selected while hidden is drawn right after returning
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _selection.Select(_resultItemId);
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PlayerInventory);
            simulate.FrameUpdate();

            var viewer = _columns.Q<RecipeViewerView>();
            Assert.IsTrue(IsShown(viewer));
            Assert.IsFalse(IsShown(Find<Label>(viewer, "recipe-viewer-hint")), "出しても未選択の古い値のまま");
            Assert.IsNotNull(viewer.Q<VisualElement>($"craft-recipe-entry-{_recipeGuid}"), "出しても隠れている間に選んだアイテムのレシピを描かない");
        }

        [Test]
        public void 出すときは隠れたまま最新の値を受けてから出し古い値で組まない()
        {
            // 選んで描いてから隠し、隠れている間に選択を外す
            // Select and draw, hide, then clear the selection while hidden
            _selection.Select(_resultItemId);
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _selection.Clear();

            // 出す間に引いたアイテムの絵は一覧のセルの分だけ（先に出すとビューアが隠す前の選択のレシピを組み、その素材と結果の絵も引く）
            // Item textures fetched while showing are the list cells only (showing first would build the stale selection's recipes and fetch their textures too)
            _textures.Reset();
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PlayerInventory);
            simulate.FrameUpdate();

            Assert.AreEqual(_model.ItemList.Value.Cells.Count, _textures.ItemTextureCount, "出す前の古い選択でビューアを組んだ");
            Assert.IsTrue(IsShown(Find<Label>(_columns.Q<RecipeViewerView>(), "recipe-viewer-hint")), "出した後が未選択の案内でない");
        }

        // アイテムの絵を引いた回数を数える出所（絵そのものは null）
        // Texture source that counts item texture lookups (the textures themselves are null)
        private sealed class CountingItemTextureSource : IGameContentTextureSource
        {
            public int ItemTextureCount { get; private set; }

            public void Reset()
            {
                ItemTextureCount = 0;
            }

            public Texture2D ItemTexture(ItemId id)
            {
                ItemTextureCount++;
                return null;
            }

            public Texture2D BlockTexture(BlockId id)
            {
                return null;
            }

            public Texture2D FluidTexture(FluidId id)
            {
                return null;
            }

            public Texture2D TrainCarTexture(Guid trainCarGuid)
            {
                return null;
            }

            public Texture2D ConnectToolTexture(Guid connectToolGuid)
            {
                return null;
            }
        }
    }
}
