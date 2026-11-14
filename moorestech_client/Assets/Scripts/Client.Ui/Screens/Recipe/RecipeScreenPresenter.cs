using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// ビューアと一覧を起動時に舞台の Viewer・Items 列へ載せ、インベントリ画面のときだけ出す（Web App.tsx の screen === "playerInventory"）
    /// Mounts the viewer and list on the Viewer and Items stage columns at startup and shows them only on the inventory screen (Web App.tsx)
    /// </summary>
    public sealed class RecipeScreenPresenter : IInitializable, IDisposable
    {
        private readonly StageColumnsLayout _columns;
        private readonly UiRoutingState _routing;
        private readonly RecipeScreenModel _model;
        private readonly RecipeViewerView _viewer;
        private readonly ItemListView _itemList;
        private readonly CompositeDisposable _subscriptions = new();

        public RecipeScreenPresenter(StageColumnsLayout columns, UiRoutingState routing, RecipeScreenModel model, GameContentPresentation content, IRecipeCommands commands, UiCommandResultToaster toaster, RecipeItemSelection selection)
        {
            _columns = columns;
            _routing = routing;
            _model = model;
            var context = new RecipeEntryPartsContext(content, commands, toaster, selection);
            _viewer = new RecipeViewerView(context);
            _itemList = new ItemListView(content, selection);
        }

        public void Initialize()
        {
            // 2 つの View を列へ載せ、先に画面を購読して出し入れを決めてからモデルを購読する（隠れた状態で組まない）
            // Mount both views, subscribe to the screen first to settle visibility, then to the model (so nothing builds while hidden)
            _columns.Get(StageColumn.Viewer).Add(_viewer);
            _columns.Get(StageColumn.Items).Add(_itemList);
            _routing.Current.Subscribe(ApplyScreen).AddTo(_subscriptions);
            _model.Viewer.Subscribe(_viewer.Render).AddTo(_subscriptions);
            _model.ItemList.Subscribe(_itemList.Render).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _viewer.RemoveFromHierarchy();
            _itemList.RemoveFromHierarchy();
        }

        private void ApplyScreen(UiRoutingSnapshot snapshot)
        {
            // 閉じると両 View が中身を捨てる／先頭へ戻す（長押しのエントリも木から外れて止まる）
            // Hiding makes both views drop their content or reset the scroll (the held entry leaves the tree and stops)
            var shown = RecipeScreenVisibility.IsShown(snapshot);
            _viewer.SetShown(shown);
            _itemList.SetShown(shown);
        }
    }
}
