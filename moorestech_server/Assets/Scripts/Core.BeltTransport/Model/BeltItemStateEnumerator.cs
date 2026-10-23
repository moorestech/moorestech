namespace Core.BeltTransport
{
    // segment の走行中アイテムを出口に近い順に、配列へ写さずその場で列挙する。出口までの距離は隙間を足しながら求める。foreach で使う
    // Enumerates a segment's running items in exit order without copying them into an array; the distance to the exit accumulates over the gaps. Used with foreach
    public struct BeltItemStateEnumerator
    {
        private readonly BeltItem[] _items;
        private readonly int[] _gaps;
        private readonly int _head;
        private readonly int _capacity;
        private readonly int _count;
        private int _index;
        private int _distance;

        internal BeltItemStateEnumerator(BeltItem[] items, int[] gaps, int head, int capacity, int count)
        {
            _items = items;
            _gaps = gaps;
            _head = head;
            _capacity = capacity;
            _count = count;
            _index = -1;
            _distance = 0;
            Current = default;
        }

        public BeltItemState Current { get; private set; }

        public BeltItemStateEnumerator GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            _index++;
            if (_count <= _index) return false;
            var p = (_head + _index) % _capacity;
            _distance += _gaps[p] + (_index == 0 ? 0 : BeltConstants.ItemWidth);
            Current = new BeltItemState(_items[p], _distance);
            return true;
        }
    }
}
