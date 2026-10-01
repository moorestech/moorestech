using Core.Master;
using Cysharp.Threading.Tasks;
namespace Client.Game.InGame.Entity.Factory
{
    public interface IBeltItemPrefabLoader
    {
        UniTask<BeltItemPrefab> LoadAsync(ItemId itemId);
    }
}
