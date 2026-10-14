using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 表示中のアイテム1個。表示物は生成時に揃っているので、位置を置くことと消すことだけを担う
    // One displayed item; its object exists from creation, so this only positions and removes it
    public sealed class BeltItemView
    {
        // 最後に見かけた更新回。これが現在の回と違えばアイテムは複製から消えている
        // The refresh in which the item was last seen; when it differs from the current one the item has left the replica
        public int SeenInRefresh { get; private set; }
        private readonly IBeltItemObject _itemObject;

        public BeltItemView(int seenInRefresh, IBeltItemObject itemObject)
        {
            SeenInRefresh = seenInRefresh;
            _itemObject = itemObject;
        }

        public void MoveTo(int refresh, Vector3 position)
        {
            SeenInRefresh = refresh;
            _itemObject.SetPosition(position);
        }

        public void Remove()
        {
            _itemObject.Destroy();
        }
    }
}
