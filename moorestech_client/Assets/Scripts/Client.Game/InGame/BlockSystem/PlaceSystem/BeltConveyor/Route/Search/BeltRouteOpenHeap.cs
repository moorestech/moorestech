using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     探索の待ち行列。f小→g大の順に出す二分ヒープ
    ///     Search open set: binary heap popping small f, then large g
    /// </summary>
    public sealed class BeltRouteOpenHeap
    {
        private readonly List<(int f, int g, BeltRouteState state)> _items = new();

        public int Count => _items.Count;

        public void Clear() => _items.Clear();

        public void Push(int f, int g, BeltRouteState state)
        {
            _items.Add((f, g, state));
            SiftUp(_items.Count - 1);

            #region Internal

            void SiftUp(int index)
            {
                while (0 < index)
                {
                    var parent = (index - 1) / 2;
                    if (!IsBefore(_items[index], _items[parent])) return;
                    (_items[index], _items[parent]) = (_items[parent], _items[index]);
                    index = parent;
                }
            }

            #endregion
        }

        public (int f, int g, BeltRouteState state) Pop()
        {
            var top = _items[0];
            var last = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (0 < _items.Count)
            {
                _items[0] = last;
                SiftDown(0);
            }
            return top;

            #region Internal

            void SiftDown(int index)
            {
                while (true)
                {
                    var left = index * 2 + 1;
                    if (_items.Count <= left) return;
                    var right = left + 1;
                    var child = right < _items.Count && IsBefore(_items[right], _items[left]) ? right : left;
                    if (!IsBefore(_items[child], _items[index])) return;
                    (_items[index], _items[child]) = (_items[child], _items[index]);
                    index = child;
                }
            }

            #endregion
        }

        private static bool IsBefore((int f, int g, BeltRouteState state) a, (int f, int g, BeltRouteState state) b)
        {
            return a.f < b.f || (a.f == b.f && b.g < a.g);
        }

    }
}
