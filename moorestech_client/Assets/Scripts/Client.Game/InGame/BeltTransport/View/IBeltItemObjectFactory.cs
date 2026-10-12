using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // アイテムの種類に合う表示物を作る。モデルの読み込みが非同期なので完成は後から届く
    // Creates the displayed object fitting the item kind; model loading is asynchronous, so the object arrives later
    public interface IBeltItemObjectFactory
    {
        UniTask<IBeltItemObject> CreateAsync(ItemId itemId, Vector3 position);
    }
}
