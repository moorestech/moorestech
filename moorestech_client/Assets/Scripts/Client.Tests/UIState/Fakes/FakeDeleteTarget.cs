using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.UI.UIState.State;
using Mooresmaster.Localization.Generated;

namespace Client.Tests.UIState.Fakes
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

        // 拒否時に返す理由キー（未設定なら「拒否だが表示すべき理由が無い」を表す）
        // Reason key returned on denial; unset means "denied without a displayable reason"
        public LocalizationKey? DenyReason;
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

        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            deniedReason = Removable ? null : DenyReason;
            return Removable;
        }

        // 返す撤去物と記録不能理由（未設定は記録なし）
        // Returned removed objects and unrecordable reasons (none records nothing)
        public readonly List<IRemovedObject> RemovedObjects = new();
        public readonly List<string> UnrecordableReasons = new();
        public readonly List<(Vector3Int Position, BlockDirection Direction, BlockId BlockId, string Reason)> UnrecordableBlocks = new();

        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            foreach (var removedObject in RemovedObjects) collector.Add(removedObject);
            foreach (var reason in UnrecordableReasons) collector.AddUnrecordable(reason, reason);
            foreach (var block in UnrecordableBlocks)
                collector.AddUnrecordableBlock(block.Position, block.Direction, block.BlockId, block.Reason);
        }

        public void Delete()
        {
            DeleteCount++;

            // 削除後は端点情報を読めなくなる状況を再現する
            // Simulate endpoint information becoming unavailable after deletion
            RemovedObjects.Clear();
            UnrecordableReasons.Clear();
            UnrecordableBlocks.Clear();
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
