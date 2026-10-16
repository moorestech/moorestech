using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // アイテムの種類に合う表示物をその場で作る。prefabは初回に同期で読んで持つ(列車のTrainCarObjectFactoryと同形)
    // Creates the displayed object fitting the item kind on the spot; prefabs are loaded synchronously on first use and kept (same shape as TrainCarObjectFactory)
    public interface IBeltItemObjectFactory
    {
        IBeltItemObject Create(ItemId itemId, Vector3 position);
    }
}
