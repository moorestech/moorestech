using Game.Block.Blocks.Machine.State;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     選択工程の抽選に使う値。役割ごとの型で、その役割に要る値だけを持つ。抽選の確定時にも同じ型で記録する
    ///     Values a draw of the selected process uses; one type per role, carrying only that role's values, and recorded in the same type when a draw is fixed
    /// </summary>
    public abstract class CleanRoomYieldPreviewResult : IRealizedOutputDrawRecord
    {
        // 種別はこのファイルの型に閉じる
        // The kinds are closed to the types in this file
        private protected CleanRoomYieldPreviewResult()
        {
        }
    }

    // 未選択か、役割表に無いレシピ
    // No recipe selected, or a recipe outside the role tables
    public sealed class NoRolePreview : CleanRoomYieldPreviewResult
    {
        public static readonly NoRolePreview Instance = new();

        private NoRolePreview()
        {
        }
    }

    // 部屋が稼働を許さない（未密閉・クラス不足）。停止理由は A7 の状態が配る
    // The room does not allow operation (not enclosed or class out); A7's state carries the halt reason
    public sealed class NotOperablePreview : CleanRoomYieldPreviewResult
    {
        public static readonly NotOperablePreview Instance = new();

        private NotOperablePreview()
        {
        }
    }

    public sealed class WaferGradePreview : CleanRoomYieldPreviewResult
    {
        public int MaxGrade { get; }
        public double PromotionChance { get; }

        public WaferGradePreview(int maxGrade, double promotionChance)
        {
            MaxGrade = maxGrade;
            PromotionChance = promotionChance;
        }
    }

    public sealed class LayerExposurePreview : CleanRoomYieldPreviewResult
    {
        public double LayerFailRate { get; }

        public LayerExposurePreview(double layerFailRate)
        {
            LayerFailRate = layerFailRate;
        }
    }

    public sealed class DicingPreview : CleanRoomYieldPreviewResult
    {
        public double DieLossRate { get; }

        public DicingPreview(double dieLossRate)
        {
            DieLossRate = dieLossRate;
        }
    }
}
