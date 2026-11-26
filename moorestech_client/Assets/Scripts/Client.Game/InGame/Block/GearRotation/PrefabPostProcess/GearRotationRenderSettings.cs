using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.PrefabPostProcess
{
    /// <summary>
    /// 歯車をシェーダーで回すのに使うシェーダー。Addressablesから読み、ビルドにシェーダーを確実に含める
    /// Shaders used to spin gears on the GPU; loaded from Addressables so the shaders are always in the build
    /// </summary>
    [CreateAssetMenu(fileName = "GearRotationRenderSettings", menuName = "moorestech/GearRotationRenderSettings")]
    public sealed class GearRotationRenderSettings : ScriptableObject
    {
        public const string AddressablePath = "Vanilla/Rendering/GearRotationRenderSettings";

        // このシェーダーのマテリアルだけを、同じ項目名を持つ回転版へ置き換えられる
        // Only materials on this shader can be swapped to the rotating variant that shares its property names
        [SerializeField] private Shader sourceLitShader;
        [SerializeField] private Shader rotatingLitShader;

        public Shader SourceLitShader => sourceLitShader;
        public Shader RotatingLitShader => rotatingLitShader;
    }
}
