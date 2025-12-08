using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.FluidsModule;
using Mooresmaster.Model.FluidsModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    /// <summary>
    ///     通信用の液体ID
    /// </summary>
    /// <remarks>
    ///     このIDは永続化されることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    /// </remarks>
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct FluidId
    {
    }
    
    public class FluidMaster : IMasterValidator
    {
        public static readonly FluidId EmptyFluidId = new(0);
        public static readonly Guid MixedFluidGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        public readonly Fluids Fluids;

        private Dictionary<FluidId, FluidMasterElement> _fluidElementTableById;
        private Dictionary<Guid, FluidId> _fluidGuidToFluidId;

        public FluidMaster(JToken jToken)
        {
            Fluids = FluidsLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            // FluidMasterは外部キー依存がないため、バリデーション成功を返す
            // FluidMaster has no external key dependencies, so return success
            errorLogs = "";
            return true;
        }

        public void Initialize()
        {
            // guidでソート
            // Sort by GUID
            var sortedFluidElements = Fluids.Data
                .OrderBy(e => e.FluidGuid)
                .ToList();

            // 予約されている混ざった液体を追加
            // Add reserved mixed fluid
            sortedFluidElements.Add(new FluidMasterElement("MixedFluid", MixedFluidGuid));

            // FluidID 0は空の液体として予約しているので、1から始める
            // Fluid ID 0 is reserved for empty fluid, so start from 1
            _fluidElementTableById = new Dictionary<FluidId, FluidMasterElement>();
            _fluidGuidToFluidId = new Dictionary<Guid, FluidId>();
            for (var i = 0; i < sortedFluidElements.Count; i++)
            {
                var fluidId = new FluidId(i + 1);
                var element = sortedFluidElements[i];

                _fluidElementTableById.Add(fluidId, element);
                _fluidGuidToFluidId.Add(element.FluidGuid, fluidId);
            }
        }

        public FluidMasterElement GetFluidMaster(FluidId fluidId)
        {
            if (!_fluidElementTableById.TryGetValue(fluidId, out var element))
            {
                throw new InvalidOperationException($"FluidElement not found. FluidId:{fluidId}");
            }
            
            return element;
        }
        
        public FluidMasterElement GetFluidMaster(Guid fluidGuid)
        {
            var fluidId = GetFluidId(fluidGuid);
            return GetFluidMaster(fluidId);
        }
        
        public FluidId GetFluidId(Guid fluidGuid)
        {
            if (fluidGuid == Guid.Empty)
            {
                return EmptyFluidId;
            }

            if (!_fluidGuidToFluidId.TryGetValue(fluidGuid, out var id))
            {
                throw new InvalidOperationException($"FluidElement not found. FluidGuid:{fluidGuid}");
            }

            return id;
        }

        public FluidId? GetFluidIdOrNull(Guid fluidGuid)
        {
            if (fluidGuid == Guid.Empty)
            {
                return EmptyFluidId;
            }

            if (!_fluidGuidToFluidId.TryGetValue(fluidGuid, out var id))
            {
                return null;
            }

            return id;
        }
        
        public IEnumerable<FluidId> GetAllFluidIds()
        {
            return _fluidElementTableById.Keys;
        }
    }
}