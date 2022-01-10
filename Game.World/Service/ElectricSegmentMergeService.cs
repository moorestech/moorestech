using System.Collections.Generic;
using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Service;

namespace World.Service
{
    public class ElectricSegmentMergeService : IElectricSegmentMergeService
    {
        private readonly IWorldElectricSegmentDatastore _electricSegmentDatastore;

        public ElectricSegmentMergeService(IWorldElectricSegmentDatastore electricSegmentDatastore)
        {
            _electricSegmentDatastore = electricSegmentDatastore;
        }

        /// <summary>
        /// 電柱に所属するセグメント同士をマージし、データストアにセットするシステム
        /// </summary>
        /// <param name="poles">マージしたい電柱</param>
        /// <returns></returns>
        public ElectricSegment MergeAndSetDatastoreElectricSegments(List<IElectricPole> poles)
        {
            //電力セグメントをリストアップ
            var electricSegments = new List<ElectricSegment>();
            foreach (var pole in poles)
            {
                var electricSegment = _electricSegmentDatastore.GetElectricSegment(pole);
                electricSegments.Add(electricSegment);
            }

            //電力セグメントをマージする
            var mergedElectricSegment = new ElectricMergeService().Merge(electricSegments);
            //マージ前のセグメントを削除する
            for (int i = 0; i < electricSegments.Count; i++)
            {
                _electricSegmentDatastore.RemoveElectricSegment(electricSegments[i]);
                electricSegments[i] = null;
            }

            //マージ後のセグメントを追加する
            _electricSegmentDatastore.SetElectricSegment(mergedElectricSegment);

            return mergedElectricSegment;
        }
    }
}