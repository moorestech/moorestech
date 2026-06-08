using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View.Object;
using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class TrainCarEntityChildrenObject : MonoBehaviour, IDeleteTarget
    {
        public TrainCarEntityObject TrainCarEntityObject { get; private set; }

        private ITrainCarVisualTarget _visualTarget;

        public void Initialize(TrainCarEntityObject trainCarEntityObject, ITrainCarVisualTarget visualTarget)
        {
            // 子rendererから列車entityと表示入口をたどれるようにする
            // Let renderer children resolve the train entity and visual entry
            TrainCarEntityObject = trainCarEntityObject;
            _visualTarget = visualTarget;
        }

        public void SetRemovePreviewing()
        {
            // 削除previewはentityを経由せずvisual targetへ直接反映する
            // Apply remove preview directly to the visual target without routing through the entity
            _visualTarget.SetMaterialMode(TrainCarVisualMaterialMode.RemovePreview);
        }

        public void ResetMaterial()
        {
            // 削除preview終了時は通常materialへ戻す
            // Restore normal materials when remove preview ends
            _visualTarget.SetMaterialMode(TrainCarVisualMaterialMode.Normal);
        }
        
        public bool IsRemovable(out string reason)
        {
            reason = null;
            return true;
        }
        
        public void Delete()
        {
            // サーバーへ列車削除を依頼する
            // Request train removal from the server
            ClientContext.VanillaApi.SendOnly.RemoveTrain(TrainCarEntityObject.TrainCarInstanceId);
        }
    }
}
