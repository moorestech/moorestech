using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class TrainCarEntityChildrenObject : MonoBehaviour, IDeleteTarget
    {
        public TrainCarEntityObject TrainCarEntityObject { get; private set; }

        private TrainCarMaterialController _materialController;

        public void Initialize(TrainCarEntityObject trainCarEntityObject, TrainCarMaterialController materialController)
        {
            // 子 renderer から車両 entity と material 制御へたどれるようにする
            // Let renderer children resolve the train entity and material controller
            TrainCarEntityObject = trainCarEntityObject;
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

        // 同一車両の全renderer子は同じTrainCarEntityObjectを指す＝論理削除単位
        // All renderer children of a car share the same TrainCarEntityObject = the logical delete unit
        public object GetDeleteTargetKey()
        {
            return TrainCarEntityObject;
        }
    }
}
