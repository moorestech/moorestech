using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    internal static class SubInventoryLoader
    {
        public static async UniTask<SubInventoryModel> Load(ISubInventorySource source, CancellationToken ct)
        {
            // 開いている対象から最新のモデルを作る
            // Build the latest model for the open source
            var response = await ClientContext.VanillaApi.Response.Inventory.GetInventory(source.InventoryIdentifier, ct);
            ct.ThrowIfCancellationRequested();
            return source.CreateModel(response);
        }
    }
}
