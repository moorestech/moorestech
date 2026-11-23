using UnityEngine;

namespace Client.Game.InGame.FluidNetwork.View
{
    // パイプ1本の水位表示。値は流体複製を見るFluidPipeWaterLevelPresenterが押し込む
    // The water-level display of one pipe; FluidPipeWaterLevelPresenter pushes the value from the fluid replica
    public class FluidPipeWaterLevelView : MonoBehaviour
    {
        private static readonly int WaterLevelProperty = Shader.PropertyToID("_WaterLevel");
        [SerializeField] private Transform waterLevelTransform;
        [SerializeField] private MeshRenderer meshRenderer;
        // meshRenderer のうち水の材質の枠。窓と同じ Renderer にまとめたパイプでは窓の後ろの枠になる
        // The water's material slot on meshRenderer; on pipes that share the renderer with the window it is the slot after the window
        [SerializeField] private int waterMaterialIndex;

        private float _waterLevel;
        private Material _waterLevelMaterial;

        private void Start()
        {
            if (meshRenderer == null) return;

            var materials = meshRenderer.sharedMaterials;
            _waterLevelMaterial = Instantiate(materials[waterMaterialIndex]);
            materials[waterMaterialIndex] = _waterLevelMaterial;
            meshRenderer.sharedMaterials = materials;

            // Start前に届いた水位があればそれを、無ければ空を表示する
            // Show a level that arrived before Start, otherwise show empty
            ApplyWaterLevel();
        }

        // 充填率(0〜1)を受け取る。共有マテリアルを書き換えないよう複製前は反映をStartへ任せる
        // Takes the fill rate (0 to 1); leaves the update to Start until the material is cloned so the shared material is never written
        public void SetWaterLevel(float waterLevel)
        {
            _waterLevel = waterLevel;
            // silent-ok: 複製前の水位はStartのApplyWaterLevelが必ず反映する / 録画テストの水位表示で気づく
            if (_waterLevelMaterial == null) return;
            ApplyWaterLevel();
        }

        private void ApplyWaterLevel()
        {
            if (waterLevelTransform == null) return;

            waterLevelTransform.localPosition = new Vector3(0.5f, (_waterLevel + 0.5f) * 0.5f, 0.5f);
            waterLevelTransform.localScale = new Vector3(Mathf.Sqrt(1 - Mathf.Pow(_waterLevel * 2 - 1, 2)) * 0.5f, 1, 1);

            // 実行中は複製した材質へ書く。Edit mode のインスペクタのシミュレーションだけ共有材質の枠へ書く
            // At runtime write to the cloned material; only the edit-mode inspector simulation writes to the shared slot
            var waterMaterial = _waterLevelMaterial != null ? _waterLevelMaterial : meshRenderer.sharedMaterials[waterMaterialIndex];
            waterMaterial.SetFloat(WaterLevelProperty, _waterLevel);
        }

        #if UNITY_EDITOR
        public float DebugWaterLevel => _waterLevel;

        // Edit modeではStartが走らず複製が無いため、インスペクタのシミュレーション用に即時反映する
        // In edit mode Start has not run and nothing is cloned, so the inspector simulation applies the level immediately
        public void DebugApplyWaterLevelImmediately(float waterLevel)
        {
            _waterLevel = waterLevel;
            ApplyWaterLevel();
        }
        #endif
    }
}
