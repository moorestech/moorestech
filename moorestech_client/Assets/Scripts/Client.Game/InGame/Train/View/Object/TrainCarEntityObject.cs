using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Common.Debug;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrainCarEntityObject : MonoBehaviour
    {
        public TrainCarInstanceId TrainCarInstanceId { get; private set; }
        public TrainCarMasterElement TrainCarMasterElement { get; set; }
        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset
        /// </summary>
        public float ModelForwardCenterOffset => _poseService.ModelForwardCenterOffset;

        private bool _debugAutoRun = false;//////////////////
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private TrainCarPoseService _poseService;
        private SeatPosition[] _seatPositions = Array.Empty<SeatPosition>();
        private MaterialPreviewState _materialPreviewState = MaterialPreviewState.Normal;

        private enum MaterialPreviewState
        {
            Normal,
            RemovePreviewing,
            PlacementOverlapPreviewing,
        }

        /// <summary>
        /// 初期化を行う
        /// Perform initialization
        /// </summary>
        public void Initialize()
        {
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);//////////////////
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            var rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbodyForContact(rigidbody);

            // 表示姿勢制御をサービスへ委譲する
            // Delegate render pose control to the service
            var renderers = GetComponentsInChildren<Renderer>(true);
            _poseService = new TrainCarPoseService(transform, renderers);
            _seatPositions = ResolveSeatPositions();

            #region Internal

            SeatPosition[] ResolveSeatPositions()
            {
                // Prefab上の座席markerを一括取得してindex順に保持する
                // Collect Prefab seat markers once and keep them ordered by index
                var markers = GetComponentsInChildren<SeatPosition>(true);
                if (markers == null || markers.Length == 0)
                {
                    return Array.Empty<SeatPosition>();
                }
                Array.Sort(markers, (left, right) => left.GetSeatIndex().CompareTo(right.GetSeatIndex()));

                // index重複はログを出して最初のmarkerだけを採用する
                // Log duplicate indexes and keep only the first marker
                var uniqueMarkers = new List<SeatPosition>(markers.Length);
                for (var i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    if (marker.GetSeatIndex() < 0)
                    {
                        Debug.LogError($"TrainCar SeatPosition has negative index. TrainCar:{name} SeatIndex:{marker.GetSeatIndex()}");
                        continue;
                    }
                    if (uniqueMarkers.Count > 0 && uniqueMarkers[uniqueMarkers.Count - 1].GetSeatIndex() == marker.GetSeatIndex())
                    {
                        Debug.LogError($"TrainCar SeatPosition index duplicated. TrainCar:{name} SeatIndex:{marker.GetSeatIndex()}");
                        continue;
                    }
                    uniqueMarkers.Add(marker);
                }
                return uniqueMarkers.ToArray();
            }

            void ConfigureRigidbodyForContact(Rigidbody targetRigidbody)
            {
                // Rigidbodyは乗車・接触検出用に限定し、列車姿勢はTransform更新で決める。
                // Restrict the Rigidbody to riding/contact detection while train pose stays Transform-driven.
                targetRigidbody.isKinematic = true;
                targetRigidbody.useGravity = false;
                targetRigidbody.interpolation = RigidbodyInterpolation.None;
                targetRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            #endregion
        }

        public void SetTrain(TrainCarInstanceId trainCarInstanceId, TrainCarMasterElement trainCarMasterElement)
        {
            TrainCarInstanceId = trainCarInstanceId;
            TrainCarMasterElement = trainCarMasterElement;
        }

        /// <summary>
        /// 物理更新で反映する列車姿勢を設定する
        /// Set the train pose to apply during physics updates
        /// </summary>
        public void SetDirectPose(Vector3 position, Quaternion rotation)
        {
            _poseService.RequestPose(position, rotation);
        }

        public bool IsPoseServiceReady()
        {
            return _poseService != null;
        }

        public bool TryGetSeatPosition(int seatIndex, out Transform seatTransform)
        {
            // cached markerから指定indexの座席Transformを探す
            // Find the requested seat Transform from cached markers
            seatTransform = null;
            if (seatIndex < 0)
            {
                return false;
            }
            for (var i = 0; i < _seatPositions.Length; i++)
            {
                if (_seatPositions[i].GetSeatIndex() != seatIndex)
                {
                    continue;
                }
                seatTransform = _seatPositions[i].transform;
                return true;
            }
            return false;
        }

        public int GetVisualPartCount()
        {
            return _poseService.GetVisualPartCount();
        }

        public bool TryGetVisualPartLengthMeters(int index, out int lengthMeters)
        {
            return _poseService.TryGetVisualPartLengthMeters(index, out lengthMeters);
        }

        public bool TryGetVisualPartModelForwardCenterOffset(int index, out float modelForwardCenterOffset)
        {
            return _poseService.TryGetVisualPartModelForwardCenterOffset(index, out modelForwardCenterOffset);
        }

        public void SetDirectPartPose(int index, Vector3 position, Quaternion rotation)
        {
            // 分割表示 part の world pose を姿勢サービスへ委譲する
            // Delegate split visual-part world pose to the pose service
            _poseService.RequestPartPose(index, position, rotation);
        }

        /// <summary>
        /// GameObject を破棄する
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            _rendererMaterialReplacerController?.DestroyMaterial();
            Destroy(gameObject);
        }

        /// <summary>
        /// 毎フレーム呼ばれ
        /// Called every frame
        /// </summary>
        private void Update()
        {
            // デバッグ用：列車の自動運転（AutoRun）の ON/OFF が変化したらサーバへ通知する
            // Notify the server when the debug train AutoRun state changes
            if (_debugAutoRun != DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
                OnTrainAutoRunChanged(_debugAutoRun);
                Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
            }
        }

        // 自動運転（AutoRun）の状態をサーバへ送信するローカル関数
        // Local function to send the auto-run state for all trains
        private void OnTrainAutoRunChanged(bool isEnabled)
        {
            // サーバへ「列車自動運転」の切り替えコマンドを送信する
            // Send the auto-run toggle command for all trains to the server
            var command = isEnabled
                ? $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOnArgument}"
                : $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOffArgument}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
        }

        public void SetRemovePreviewing()
        {
            // 同じ削除プレビュー状態では材質差し替えを繰り返さない
            // Avoid repeated material replacement while already in remove preview
            if (_materialPreviewState != MaterialPreviewState.RemovePreviewing)
            {
                var placePreviewMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
                _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
                _materialPreviewState = MaterialPreviewState.RemovePreviewing;
            }
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        // 事実上、新規でTrainCarを設置しようとしたときに連結できますよを視覚的に知らせるための表示のみ用
        // This preview only visually indicates that a new TrainCar can be connected
        public void SetPlacementOverlapPreviewing()
        {
            // 同じ設置重複ハイライトでは材質差し替えを繰り返さない
            // Avoid repeated material replacement while already in placement overlap preview
            if (_materialPreviewState != MaterialPreviewState.PlacementOverlapPreviewing)
            {
                var placePreviewMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
                _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
                _materialPreviewState = MaterialPreviewState.PlacementOverlapPreviewing;
            }
            // 設置候補重複ハイライトは設置可能色(青)で表示する
            // Show placement-overlap highlight in placeable color (blue)
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.PlaceableColor);
        }

        public void ResetMaterial()
        {
            if (_materialPreviewState == MaterialPreviewState.Normal)
            {
                return;
            }
            _rendererMaterialReplacerController.ResetMaterial();
            _materialPreviewState = MaterialPreviewState.Normal;
        }
    }
}
