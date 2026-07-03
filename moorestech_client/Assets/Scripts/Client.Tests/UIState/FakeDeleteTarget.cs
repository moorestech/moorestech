using Client.Game.Common;
using Client.Game.InGame.UI.UIState.State;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     呼び出し回数を記録するIDeleteTargetのテスト用実装
    ///     Test implementation of IDeleteTarget that records call counts
    /// </summary>
    public class FakeDeleteTarget : IDeleteTarget
    {
        public int SetPreviewCount;
        public int ResetCount;
        public int DeleteCount;
        public bool Removable;
        public object Key;
        public string Category = BlockMasterElementExtension.DefaultDestructionCategory;

        public void SetRemovePreviewing()
        {
            SetPreviewCount++;
        }

        public void ResetMaterial()
        {
            ResetCount++;
        }

        public bool IsRemovable(out string reason)
        {
            reason = null;
            return Removable;
        }

        public void Delete()
        {
            DeleteCount++;
        }

        public object GetDeleteTargetKey()
        {
            // Key未指定なら自身を一意キーとする（既存テストは個別インスタンス＝個別キー）
            // Default to self as the unique key when Key is unset (existing tests use per-instance keys)
            return Key ?? this;
        }

        public string GetDestructionCategory()
        {
            return Category;
        }
    }
}
