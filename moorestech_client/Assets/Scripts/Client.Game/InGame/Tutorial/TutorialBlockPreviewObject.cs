using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    /// チュートリアル専用のブロックプレビューオブジェクト
    /// Tutorial-specific block preview object
    /// </summary>
    public class TutorialBlockPreviewObject : MonoBehaviour
    {
        public BlockMasterElement BlockMasterElement { get; private set; }

        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private LoadedAsset<Material> _loadedMaterial;

        /// <summary>
        /// 非同期でマテリアルをロードして初期化
        /// Asynchronously load material and initialize
        /// </summary>
        public async UniTask InitializeAsync(BlockId blockId)
        {
            BlockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // Addressableからチュートリアル用マテリアルを非同期ロード
            // Load tutorial material asynchronously from Addressable
            _loadedMaterial = await AddressableLoader.LoadAsync<Material>(MaterialConst.TutorialPreviewBlockMaterialPath);

            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            _rendererMaterialReplacerController.CopyAndSetMaterial(_loadedMaterial.Asset);

            SetPlaceableColor(true);
        }

        /// <summary>
        /// 配置可能/不可の色を設定
        /// Set placeable/not-placeable color
        /// </summary>
        public void SetPlaceableColor(bool isPlaceable)
        {
            var color = isPlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, color);
        }

        /// <summary>
        /// GameObjectのアクティブ状態を設定
        /// Set GameObject active state
        /// </summary>
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        /// <summary>
        /// 位置と回転を設定
        /// Set position and rotation
        /// </summary>
        public void SetTransform(Vector3 pos, Quaternion rotation)
        {
            transform.position = pos;
            transform.rotation = rotation;
        }

        /// <summary>
        /// GameObjectとマテリアルを破棄
        /// Destroy GameObject and material
        /// </summary>
        public void DestroyPreview()
        {
            _rendererMaterialReplacerController.DestroyMaterial();
            _loadedMaterial?.Dispose();
            Destroy(gameObject);
        }
    }
}
