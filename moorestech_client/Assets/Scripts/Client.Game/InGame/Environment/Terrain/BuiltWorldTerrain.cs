using System.Collections.Generic;
using Game.MapGeneration.Facade;

namespace Client.Game.InGame.Environment.Terrain
{
    /// <summary>
    ///     ワールド生成が建てた地形の結果。Terrainsは今回建てたものだけで、シーンに既にある無関係な地形を含まない
    ///     The result of standing up a world's terrain; Terrains holds only what this build created, never unrelated terrain already in the scene
    /// </summary>
    public sealed class BuiltWorldTerrain
    {
        public WorldTerrainLayout Layout { get; }
        public IReadOnlyList<UnityEngine.Terrain> Terrains { get; }

        public BuiltWorldTerrain(WorldTerrainLayout layout, IReadOnlyList<UnityEngine.Terrain> terrains)
        {
            Layout = layout;
            Terrains = terrains;
        }
    }
}
