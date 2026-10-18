using System.Threading;
using Client.Game.InGame.UI.Inventory;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    internal interface ISubInventoryQuery
    {
        UniTask<SubInventoryModel> Load(ISubInventorySource source, CancellationToken ct);
    }
}
