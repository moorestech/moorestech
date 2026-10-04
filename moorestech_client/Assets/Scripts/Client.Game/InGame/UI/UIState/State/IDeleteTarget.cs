using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     削除可能なオブジェクトを表すインターフェース
    ///     Interface representing an object that can be deleted
    /// </summary>
    public interface IDeleteTarget
    {
        /// <summary>
        ///     RemovePreviewの表示
        ///     Display the remove preview
        /// </summary>
        void SetRemovePreviewing();

        /// <summary>
        ///     Materialをもとに戻す
        ///     Reset material to original state
        /// </summary>
        void ResetMaterial();
        
        /// <summary>
        ///     Remove可能かどうか（削除不可かつ表示すべき理由があるときだけ理由キーが埋まり、無いときはnull）
        ///     Whether this can be removed; the reason key is filled only when it cannot and a reason is displayable, null otherwise
        /// </summary>
        bool IsRemovable(out LocalizationKey? deniedReason);
        
        /// <summary>
        ///     実際に対象を削除する
        ///     Delete the target object
        /// </summary>
        void Delete();

        // 自身と撤去に巻き込まれる物を削除送信前に記録する
        // Record self and objects removed alongside it before sending deletion
        void CollectRemovedObjects(RemovedObjectCollector collector);

        /// <summary>
        ///     論理削除対象を一意に表すキー（同一機械・車両・レールedgeの重複選択を排除するため）
        ///     Key identifying the logical delete target, used to dedupe duplicate selection of the same machine/train/rail edge
        /// </summary>
        object GetDeleteTargetKey();

        /// <summary>
        ///     破壊カテゴリー（同一破壊セッションで混在させないための区別。未設定はdefault）
        ///     Destruction category used to prevent mixing categories in one destroy session (unset means default)
        /// </summary>
        string GetDestructionCategory();
    }
}