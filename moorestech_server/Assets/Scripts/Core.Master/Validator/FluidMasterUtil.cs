using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.FluidsModule;

namespace Core.Master.Validator
{
    public static class FluidMasterUtil
    {
        public static bool Validate(Fluids fluids, out string errorLogs)
        {
            // 予約MixedFluidはマスタJSONが定義を持つ前提。欠落を補完せずロード失敗として報告する
            // The reserved MixedFluid must be defined in the master JSON; a missing entry fails the load instead of being filled in
            if (fluids.Data.All(e => e.FluidGuid != FluidMaster.MixedFluidGuid))
            {
                errorLogs = $"fluids.json does not define the reserved MixedFluid ({FluidMaster.MixedFluidGuid})";
                return false;
            }

            // 外部キー依存はこれ以外に無い
            // There are no other external key dependencies
            errorLogs = "";
            return true;
        }

        public static void Initialize(
            Fluids fluids,
            Guid mixedFluidGuid,
            out Dictionary<FluidId, FluidMasterElement> fluidElementTableById,
            out Dictionary<Guid, FluidId> fluidGuidToFluidId)
        {
            // 予約液体を除いてguidでソート
            // Sort by GUID, excluding the reserved fluid
            var sortedFluidElements = fluids.Data
                .Where(e => e.FluidGuid != mixedFluidGuid)
                .OrderBy(e => e.FluidGuid)
                .ToList();

            // 予約されている混ざった液体はマスタJSONが定義を持ち、ここでは末尾固定の順序だけを担う
            // The reserved mixed fluid is defined in the master JSON; here we only pin it to the last position
            var mixedFluidElement = fluids.Data.First(e => e.FluidGuid == mixedFluidGuid);
            sortedFluidElements.Add(mixedFluidElement);

            // FluidID 0は空の液体として予約しているので、1から始める
            // Fluid ID 0 is reserved for empty fluid, so start from 1
            fluidElementTableById = new Dictionary<FluidId, FluidMasterElement>();
            fluidGuidToFluidId = new Dictionary<Guid, FluidId>();
            for (var i = 0; i < sortedFluidElements.Count; i++)
            {
                var fluidId = new FluidId(i + 1);
                var element = sortedFluidElements[i];

                fluidElementTableById.Add(fluidId, element);
                fluidGuidToFluidId.Add(element.FluidGuid, fluidId);
            }
        }
    }
}
