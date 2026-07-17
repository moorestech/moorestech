using System;
using System.Collections.Generic;
using Core.Master;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     パイプ永続化用の表現。内容量と流体GUIDに加え、正準側(NodeA)として所有する面の速度を方向つきで保存する。
    ///     FluidIdは揮発値なので安定したGUIDで保存する。
    ///
    ///     Persistence shape for a pipe. Besides amount and fluid GUID, it stores velocities of faces the pipe owns as the canonical NodeA side, keyed by direction.
    ///     FluidId is volatile, so the stable GUID is persisted instead.
    /// </summary>
    public class FluidPipeSaveJsonObject
    {
        [JsonProperty("fluidGuid")]
        public string FluidGuidStr;

        [JsonProperty("amount")]
        public double Amount;

        [JsonProperty("faceVelocities")]
        public List<FaceVelocityJsonObject> FaceVelocities = new();

        [JsonIgnore]
        public FluidId FluidId => MasterHolder.FluidMaster.GetFluidId(string.IsNullOrEmpty(FluidGuidStr) ? Guid.Empty : Guid.Parse(FluidGuidStr));

        public Dictionary<Vector3Int, double> ToFaceVelocityDictionary()
        {
            var result = new Dictionary<Vector3Int, double>();
            foreach (var face in FaceVelocities)
            {
                result[new Vector3Int(face.X, face.Y, face.Z)] = face.Velocity;
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

            [JsonProperty("velocity")]
            public double Velocity;
        }
    }
}
