using System;
using UniRx;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Game.InGame.UI.UIState.State;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class TrainCarEntityChildrenObject : MonoBehaviour, IDeleteTarget
    {
        private readonly Subject<DeleteCompletion> _deleteCompleted = new();
        public IObservable<DeleteCompletion> OnDeleteCompleted => _deleteCompleted;

        public TrainCarEntityObject TrainCarEntityObject { get; private set; }

        private TrainCarMaterialController _materialController;

        public void Initialize(TrainCarEntityObject trainCarEntityObject, TrainCarMaterialController materialController)
        {
            // 子 renderer から車両 entity と material 制御へたどれるようにする
            // Let renderer children resolve the train entity and material controller
            TrainCarEntityObject = trainCarEntityObject;
            trainCarEntityObject.RegisterDeleteTarget(this);
            _materialController = materialController;
        }

        public void SetRemovePreviewing()
        {
            // 削除 preview は entity を経由せず material controller へ直接反映する
            // Apply remove preview directly to the material controller without routing through the entity
            _materialController.SetMaterialMode(TrainCarVisualMaterialMode.RemovePreview);
        }

        public void ResetMaterial()
        {
            // 削除 preview 終了時は通常 material へ戻す
            // Restore normal materials when remove preview ends
            _materialController.SetMaterialMode(TrainCarVisualMaterialMode.Normal);
        }

        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            deniedReason = null;
            return true;
        }

        // 車両の撤去はUndo対象外（ADR 0076の範囲外）なので何も積まない
        // Train car removal is outside undo (out of ADR 0076 scope), so nothing is appended
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
        }

        public void Delete()
        {
            // サーバーへ列車削除を依頼する
            // Request train removal from the server
            ClientDIContext.TrainCarRemovalRequests.Remove(TrainCarEntityObject.TrainCarInstanceId, GetDeleteTargetKey()).Subscribe(_deleteCompleted.OnNext);
        }

        // 復元を共有する表示の寿命を識別する
        // Identify the lifetime of the shared preview view
        public object GetPreviewResetKey() => _materialController;

        // 同一車両の全renderer子は同じ永続IDを返し、view再生成でも対象を取り違えない
        // Every renderer child returns the stable car id so recreating a view cannot change the removal identity
        public object GetDeleteTargetKey()
        {
            return TrainCarEntityObject.TrainCarInstanceId;
        }

        // 車両はカテゴリー対象外なのでdefault扱い
        // Train cars are not categorized, so treat them as default
        public string GetDestructionCategory()
        {
            return BlockMasterElementExtension.DefaultDestructionCategory;
        }
    }
}
