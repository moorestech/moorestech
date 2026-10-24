using System;
using System.Collections.Generic;
using Core.Master;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     パイプ永続化用の表現(セーブ版 7)。内容量と流体GUIDに加え、正準側(NodeA)として所有する面の速度を方向つきで保存する。
    ///     量と速度は FluidUnits の整数単位(10 億分の 1)。版 6 の double(`amount`・`velocity`)は SaveMigrationStepV6ToV7 が変換する。FluidIdは揮発値なので安定したGUIDで保存する。
    ///
    ///     Persistence shape for a pipe (save version 7). Besides amount and fluid GUID, it stores velocities of faces the pipe owns as the canonical NodeA side, keyed by direction.
    ///     Amount and velocity are FluidUnits integers (10^-9 of an amount); the version-6 doubles (`amount`, `velocity`) are converted by SaveMigrationStepV6ToV7. FluidId is volatile, so the stable GUID is persisted instead.
    /// </summary>
    public class FluidPipeSaveJsonObject
    {
        [JsonProperty("fluidGuid")]
        public string FluidGuidStr;

        [JsonProperty("amountNano")]
        public long AmountUnits;

        [JsonProperty("faceVelocities")]
        public List<FaceVelocityJsonObject> FaceVelocities = new();

        [JsonIgnore]
        public FluidId FluidId => MasterHolder.FluidMaster.GetFluidId(string.IsNullOrEmpty(FluidGuidStr) ? Guid.Empty : Guid.Parse(FluidGuidStr));

        public Dictionary<Vector3Int, long> ToFaceVelocityDictionary()
        {
            var result = new Dictionary<Vector3Int, long>();
            foreach (var face in FaceVelocities)
            {
                result[new Vector3Int(face.X, face.Y, face.Z)] = face.VelocityUnits;
            }
            return result;
        }

        public class FaceVelocityJsonObject
        {
            [JsonProperty("x")]
            public int X;

            [JsonProperty("y")]
            public int Y;

            [JsonProperty("z")]
            public int Z;

            [JsonProperty("velocityNano")]
            public long VelocityUnits;
        }
    }
}
