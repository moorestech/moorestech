using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地表コライダーが高さを持つ格子の1段（メートル）を答える
    ///     Answers one height lattice step (meters) of the ground collider that was hit
    /// </summary>
    public static class GroundHeightQuantization
    {
        // TerrainColliderは実地形のTerrainDataの16bit格子1段、それ以外の地面は量子化されないので0
        // A TerrainCollider yields one 16-bit step of its actual TerrainData; other ground is not quantized, so 0
        public static float StepOf(Collider groundCollider)
        {
            if (groundCollider is not TerrainCollider terrainCollider) return 0f;
            return TerrainHeightStorage.StepMeters(terrainCollider.terrainData.size.y);
        }
    }
}
