using System;
using System.Collections.Generic;
using Core.Electric;
using Game.World.Interface.DataStore;
using World.Event;

namespace World.DataStore
{
    public class WorldElectricSegmentDatastore : IWorldElectricSegmentDatastore
    {
        private readonly List<ElectricSegment> _segmentDictionary = new();

        public WorldElectricSegmentDatastore()
        {
            
        }
        
        //電柱オブジェクトから所属している電力セグメントを取得する
        public ElectricSegment GetElectricSegment(IElectricPole pole)
        {
            foreach (var segment in _segmentDictionary)
            {
                if (!segment.ExistElectricPole(pole.GetIntId())) continue;
                return segment;
            }
            throw new Exception("電力セグメントが見つかりませんでした");
        }

        public ElectricSegment GetElectricSegment(int index)
        {
            return _segmentDictionary[index];   
        }

        public ElectricSegment CreateElectricSegment()
        {
            var electricSegment = new ElectricSegment();
            _segmentDictionary.Add(electricSegment);
            return electricSegment;
        }

        public ElectricSegment MergedElectricSegments(List<IElectricPole> poles)
        {
            //電力セグメントをリストアップ
            var electricSegments = new List<ElectricSegment>();
            foreach (var pole in poles)
            {
                var electricSegment = GetElectricSegment(pole);
                electricSegments.Add(electricSegment);
            }
            
            //電力セグメントをマージする
            var mergedElectricSegment = new ElectricMergeService().Merge(electricSegments);
            //マージ前のセグメントを削除する
            foreach (var electricSegment in electricSegments)
            {
                _segmentDictionary.Remove(electricSegment);
            }
            //マージ後のセグメントを追加する
            _segmentDictionary.Add(mergedElectricSegment);

            return mergedElectricSegment;
        }

        public int GetElectricSegmentListCount() { return _segmentDictionary.Count; }
    }
}