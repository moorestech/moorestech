using System.Collections.Generic;
using Game.EnergySystem;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class ElectricSegmentMergeService
    {
        /// <summary>
        ///     電柱に所属するセグメント同士をマージし、データストアにセットするシステム
        /// </summary>
        /// <param name="segmentDatastore"></param>
        /// <param name="poles">マージしたい電柱</param>
        /// <returns></returns>
        public static EnergySegment MergeAndSetDatastoreElectricSegments<TSegment>(
            IWorldEnergySegmentDatastore<TSegment> segmentDatastore, List<IElectricTransformer> poles)
            where TSegment : EnergySegment, new()
        {
            //電力セグメントをリストアップ
            var electricSegments = new List<TSegment>();
            foreach (var pole in poles)
            {
                var electricSegment = segmentDatastore.GetEnergySegment(pole);
                electricSegments.Add(electricSegment);
            }
            
            //電力セグメントをマージする
            var mergedElectricSegment = EnergySegmentExtension.Merge(electricSegments);
            //マージ前のセグメントを削除する
            for (var i = 0; i < electricSegments.Count; i++)
            {
                segmentDatastore.RemoveEnergySegment(electricSegments[i]);
                electricSegments[i] = null;
            }
            
            //マージ後のセグメントを追加する
            segmentDatastore.SetEnergySegment(mergedElectricSegment);
            
            return mergedElectricSegment;
        }
    }
}