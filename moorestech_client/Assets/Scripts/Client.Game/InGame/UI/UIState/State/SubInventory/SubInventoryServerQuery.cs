using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    internal sealed class SubInventoryServerQuery : ISubInventoryQuery
    {
        public async UniTask<SubInventoryModel> Load(ISubInventorySource source, CancellationToken ct)
        {
            // キャンセルした画面の応答でモデルを変更しない
            // Do not build a model from a canceled screen's response
            var response = await ClientContext.VanillaApi.Response.Inventory.GetInventory(source.InventoryIdentifier, ct);
            ct.ThrowIfCancellationRequested();
            return source.CreateModel(response);
        }
    }
}
