using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    internal class BlueprintPasteChainPreview
    {
        private readonly GearChainPreviewLine _line;

        public BlueprintPasteChainPreview(Transform parent)
        {
            _line = new GearChainPreviewLine(parent);
        }

        public void Draw(Vector3 start, Vector3 end, bool placeable)
        {
            // 描画規則は通常延長と共通の実装へ委譲する
            // Delegate rendering rules to the implementation shared with pole extension
            _line.Draw(start, end, placeable);
        }

        public void SetActive(bool active)
        {
            _line.SetActive(active);
        }
    }
}
