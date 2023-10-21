using System.Collections.Generic;
using Core.EnergySystem;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public static class ElectricSegmentMergeService
    {

        ///     

        /// <param name="segmentDatastore"></param>
        /// <param name="poles"></param>
        /// <returns></returns>
        public static EnergySegment MergeAndSetDatastoreElectricSegments<TSegment>(IWorldEnergySegmentDatastore<TSegment> segmentDatastore, List<IEnergyTransformer> poles) where TSegment : EnergySegment, new()
        {
            
            var electricSegments = new List<TSegment>();
            foreach (var pole in poles)
            {
                var electricSegment = segmentDatastore.GetEnergySegment(pole);
                electricSegments.Add(electricSegment);
            }

            
            var mergedElectricSegment = EnergySegmentExtension.Merge(electricSegments);
            
            for (var i = 0; i < electricSegments.Count; i++)
            {
                segmentDatastore.RemoveEnergySegment(electricSegments[i]);
                electricSegments[i] = null;
            }

            
            segmentDatastore.SetEnergySegment(mergedElectricSegment);

            return mergedElectricSegment;
        }
    }
}