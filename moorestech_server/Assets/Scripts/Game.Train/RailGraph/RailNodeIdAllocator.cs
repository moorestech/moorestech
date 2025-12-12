using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// RailNodeのIDで次に使いたいIDを管理するクラス
    /// 基本的にIDは若い順に払い出されるが、解放されたIDは再利用される
    /// 次に制約として表裏nodeはnodeid^1でアクセスできるようにしたいので、偶数から連番に払い出されるようにした
    /// なのでRent()では2の倍数のIDを払い出す
    /// 例：0,1,2,3,4,5...と払い出された後に2と3が解放された場合、次にRent()を呼ぶと2が返される。Rent2()を呼ぶと2,3が返される
    /// 例：0,1,2,3,4,5...と払い出された後に3だけが解放された場合、次にRent()を呼ぶと6が返される。その後に2が解放された場合、次にRent()を呼ぶと2が返される。その次のRent()を呼ぶと3でも7でもなく8が返される
    /// </summary>
    /// 
    class RailNodeIdAllocator
    {
        private readonly MinHeap<int> _releasedIds;//本当のidではなく、2で割った値を格納していることに注意
        private readonly Action<int> _onSequentialIdAllocated;
        private int _nextSequentialId;
        private HashSet<int> _rentIds;

        public RailNodeIdAllocator(Action<int> onSequentialIdAllocated)
        {
            _releasedIds = new MinHeap<int>();
            _onSequentialIdAllocated = onSequentialIdAllocated;
            _rentIds = new HashSet<int>();
            _nextSequentialId = 0;
        }

        // 表裏railnode分まとめてレンタルする
        // Rent two ids (for both "front" and "back" rail nodes)
        public (int, int) Rent2()
        {
            if (_releasedIds.IsEmpty)
            {
                var id = _nextSequentialId;
                _nextSequentialId += 2; // 次は2の倍数にする
                _onSequentialIdAllocated(id + 2);
                _rentIds.Add(id);
                _rentIds.Add(id + 1);
                return (id, id + 1);
            }
            var i = _releasedIds.RemoveMin() * 2;
            _rentIds.Add(i);
            _rentIds.Add(i + 1);
            return (i, i + 1);
        }

        // 1つぶん。表だけrailnode版
        // Rent a single id (for the "front" rail node)
        public int Rent()
        {
            if (_releasedIds.IsEmpty)
            {
                var id = _nextSequentialId;
                _nextSequentialId += 2; // 次は2の倍数にする
                _onSequentialIdAllocated(id + 2);
                _rentIds.Add(id);
                return id;
            }
            var i = _releasedIds.RemoveMin() * 2;
            _rentIds.Add(i);
            return i;
        }

        // 使用済みIDを再利用できるように戻す
        // Return a used id to allow future reuse
        public void Return(int nodeId)
        {
            if (_rentIds.Contains(nodeId)) 
            {
                _rentIds.Remove(nodeId);
                var i = nodeId ^ 1;
                if (!_rentIds.Contains(i))//表裏両方が解放されている場合のみ再利用可能にする
                {
                    _releasedIds.Insert(nodeId / 2);
                }
            }
        }
    }
}
