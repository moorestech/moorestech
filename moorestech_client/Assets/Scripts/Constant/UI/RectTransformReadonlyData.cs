using System;
using UnityEngine;

namespace Constant.UI
{
    /// <summary>
    ///     RectTransformのデータは欲しいがアクセスはしたくない時に使う
    /// </summary>
    public class RectTransformReadonlyData
    {
        /// <summary>
        ///     RectTransformの位置が変わった時のために保持しておく
        /// </summary>
        private readonly RectTransform _dataTargetTransform;


        public RectTransformReadonlyData(RectTransform dataTargetTransform)
        {
            _dataTargetTransform = dataTargetTransform;
        }

        public bool IsDestroyed => _dataTargetTransform == null;


        /// <summary>
        ///     指定された<see cref="RectTransform" />を、このオブジェクトが保持している値と同じ値にします
        /// </summary>
        /// <param name="syncTargetTransform">変更される<see cref="RectTransform" /></param>
        public void SyncRectTransform(RectTransform syncTargetTransform)
        {
            if (IsDestroyed) throw new Exception("RectTransformReadonlyDataのRectTransformが破棄されています");

            syncTargetTransform.gameObject.SetActive(_dataTargetTransform.gameObject.activeInHierarchy);

            //一旦SyncTargetをDataTargetの親に変更する
            //一旦親を変更し、また親を戻すことによって、ローカル座標を正しく反映することができる
            var tmpParent = syncTargetTransform.parent;
            syncTargetTransform.SetParent(_dataTargetTransform.parent);

            //変更した上で、データを反映する
            syncTargetTransform.position = _dataTargetTransform.position;
            syncTargetTransform.rotation = _dataTargetTransform.rotation;
            syncTargetTransform.localScale = _dataTargetTransform.localScale;

            syncTargetTransform.pivot = _dataTargetTransform.pivot;
            syncTargetTransform.anchoredPosition = _dataTargetTransform.anchoredPosition;
            syncTargetTransform.anchorMax = _dataTargetTransform.anchorMax;
            syncTargetTransform.anchorMin = _dataTargetTransform.anchorMin;
            syncTargetTransform.offsetMax = _dataTargetTransform.offsetMax;
            syncTargetTransform.offsetMin = _dataTargetTransform.offsetMin;
            syncTargetTransform.sizeDelta = _dataTargetTransform.sizeDelta;
            syncTargetTransform.anchoredPosition3D = _dataTargetTransform.anchoredPosition3D;

            //元の親に戻す
            syncTargetTransform.SetParent(tmpParent);
        }
    }
}