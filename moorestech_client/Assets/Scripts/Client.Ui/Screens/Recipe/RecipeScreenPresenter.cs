using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// ビューアと一覧を起動時に舞台の Viewer・Items 列へ載せ、インベントリ画面のときだけ出す（Web App.tsx の screen === "playerInventory"）
    /// Mounts the viewer and list on the Viewer and Items stage columns at startup and shows them only on the inventory screen (Web App.tsx)
    /// </summary>
    public sealed class RecipeScreenPresenter : IInitializable
    {
        private readonly StageColumnsLayout _columns;
        private readonly UiRoutingState _routing;
        private readonly RecipeScreenModel _model;
        private readonly RecipeViewerView _viewer;
        private readonly ItemListView _itemList;
        private readonly SerialDisposable _modelSubscription = new();

        public RecipeScreenPresenter(StageColumnsLayout columns, UiRoutingState routing, RecipeScreenModel model, GameContentPresentation content, IRecipeCommands commands, RecipeItemSelection selection)
        {
            _columns = columns;
            _routing = routing;
            _model = model;
            var context = new RecipeEntryPartsContext(content, commands, selection);
            _viewer = new RecipeViewerView(context);
            _itemList = new ItemListView(content, selection);
        }

        public void Initialize()
        {
            // 2 つの View を列へ載せ、表示中だけモデルを購読する（§4.2 の ①。隠れた状態で組まない）
            // Mount both views and follow the model only while shown (§4.2 type 1; nothing builds while hidden)
            _columns.Get(StageColumn.Viewer).Add(_viewer);
            _columns.Get(StageColumn.Items).Add(_itemList);
            _routing.Current.Select(RecipeScreenVisibility.IsShown).DistinctUntilChanged().Subscribe(ApplyScreen);

            #region Internal

            void ApplyScreen(bool shown)
            {
                // 出すときは購読（最新値で 1 回描く）を先に張ってから出す。ビューアは隠れている間に受けた値を覚えるだけなので、出した時に 1 回だけ組む
                // On show, subscribe first (one draw from the latest) and then show; the viewer only remembers values while hidden, so it builds once on show
                if (shown) _modelSubscription.Disposable = new CompositeDisposable(_model.Viewer.Subscribe(_viewer.Render), _model.ItemList.Subscribe(_itemList.Render));

                // 閉じると両 View が中身を捨てる／先頭へ戻す（長押しのエントリも木から外れて止まる）
                // Hiding makes both views drop their content or reset the scroll (the held entry leaves the tree and stops)
                _viewer.SetShown(shown);
                _itemList.SetShown(shown);
                if (!shown) _modelSubscription.Disposable = Disposable.Empty;
            }

            #endregion
        }
    }
}
