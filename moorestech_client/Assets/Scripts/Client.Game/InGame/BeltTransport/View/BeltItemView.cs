using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 表示中のアイテム1個。表示物は後から届くので、届くまでは位置だけを覚え、届く前に消えたら届いた表示物をその場で消す
    // One displayed item; the object arrives later, so only the position is kept until then, and an object arriving after removal is destroyed on the spot
    public sealed class BeltItemView
    {
        // 最後に見かけた更新回。これが現在の回と違えばアイテムは複製から消えている
        // The refresh in which the item was last seen; when it differs from the current one the item has left the replica
        public int SeenInRefresh { get; private set; }
        private Vector3 _position;
        private IBeltItemObject _itemObject;
        private bool _isRemoved;

        public BeltItemView(int seenInRefresh, Vector3 position)
        {
            SeenInRefresh = seenInRefresh;
            _position = position;
        }

        public void MoveTo(int refresh, Vector3 position)
        {
            SeenInRefresh = refresh;
            _position = position;
            _itemObject?.SetPosition(position);
        }

        public void Attach(IBeltItemObject itemObject)
        {
            if (_isRemoved)
            {
                itemObject.Destroy();
                return;
            }
            _itemObject = itemObject;
            _itemObject.SetPosition(_position);
        }

        public void Remove()
        {
            _isRemoved = true;
            _itemObject?.Destroy();
            _itemObject = null;
        }
    }
}
