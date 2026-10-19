using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     箱の意図（追加／除外）ごとの表示一式。件数と0件理由の文言キー、ログ上の対象名、範囲の色と可視化オブジェクトを持つ
    ///     Per-intent (add / remove) box presentation: count and empty-range text keys, the target label for logs, the range color and its visualizer
    /// </summary>
    internal sealed class DeleteBoxIntentView
    {
        internal static DeleteBoxIntentView CreateAdd() => new("DeleteBox", MaterialConst.NotPlaceableColor, LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange, LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange, "removable");
        // 除外の箱は追加の赤と見分けるため設置可の青で描く
        // A remove box is drawn in the placeable blue to tell it from the red add box
        internal static DeleteBoxIntentView CreateRemove() => new("DeleteDeselectBox", MaterialConst.PlaceableColor, LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange, LocalizationKeys.Ui.Tooltip.DeleteBoxNoSelectedInRange, "selected");

        internal LocalizationKey CountKey { get; }
        internal LocalizationKey EmptyRangeKey { get; }
        internal string TargetLabelForLog { get; }
        private readonly string _objectNamePrefix;
        private readonly Color _rangeColor;
        private CellRangeBoxVisualizer _visualizer;

        private DeleteBoxIntentView(string objectNamePrefix, Color rangeColor, LocalizationKey countKey, LocalizationKey emptyRangeKey, string targetLabelForLog)
        {
            _objectNamePrefix = objectNamePrefix;
            _rangeColor = rangeColor;
            CountKey = countKey;
            EmptyRangeKey = emptyRangeKey;
            TargetLabelForLog = targetLabelForLog;
        }

        // 実際に表示するときだけ可視化オブジェクトを作る
        // Create the visualizer only when something is actually shown
        internal CellRangeBoxVisualizer GetVisualizer() => _visualizer ??= new CellRangeBoxVisualizer(_objectNamePrefix, _rangeColor);

        internal void Hide() => _visualizer?.HideAll();
    }
}
