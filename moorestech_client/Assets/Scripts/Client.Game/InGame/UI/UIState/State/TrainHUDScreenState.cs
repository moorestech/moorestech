using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.UI.UIState.State.TrainHUDScreen;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    // 列車に乗車中の HUD ステート。State が列車関連処理の唯一の起点である
    // Train HUD state, the single source of truth for all train-related processing while riding.
    public class TrainHUDScreenState : IUIState
    {
        private const float TrainInputHeartbeatIntervalSeconds = 2f;
        private const int BranchRoutePreviewSearchNodeLimit = 3;
        private const int BranchRoutePreviewSamplesPerSegment = 18;
        private const float BranchRoutePreviewHeightOffset = 0.22f;
        private const float BranchRoutePreviewWidth = 0.18f;
        private static readonly Color BranchRoutePreviewColor = new(1f, 0.78f, 0.18f, 0.92f);

        private readonly PlayerStateController _playerStateController;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly TrainHudScreenUIStateController _subStateController;
        private readonly List<IRailNode> _branchRouteNodes = new();
        private readonly List<IRailNode> _branchCandidateNodes = new();
        private readonly List<Vector3> _branchRoutePoints = new();

        private bool _isDismountTrain = false;
        private RidingPlayerStateContext _rideContext;
        private bool _hasSentTrainMoveInput;
        private bool _lastSentMoveForward;
        private bool _lastSentMoveBackward;
        private float _lastTrainInputSentAt;
        private GameObject _branchRoutePreviewObject;
        private LineRenderer _branchRoutePreviewLine;
        private Material _branchRoutePreviewMaterial;

        private IDisposable _eventSubscription;
        private CancellationTokenSource _cts;


        public TrainHUDScreenState(PlayerStateController playerStateController, TrainUnitClientCache trainUnitClientCache, InGameCameraController inGameCameraController, PauseMenuStateService pauseMenuStateService)
        {
            _playerStateController = playerStateController;
            _trainUnitClientCache = trainUnitClientCache;
            _subStateController = new TrainHudScreenUIStateController(pauseMenuStateService, inGameCameraController);
        }

        public void OnEnter(UITransitContext context)
        {
            // 入れ子サブステートを初期化（GameScreenから開始）
            // Initialize the nested sub-state controller (starts at GameScreen).
            _subStateController.StartSubState();
            
            // サーバー強制降車イベントを購読する。HUDに居る間だけ反映
            // Subscribe to server-forced dismount events; only applied while this HUD is active.
            _eventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RidingStateEventPacket.EventTag, OnRidingStateEventReceived);
            
            _rideContext = null;
            _isDismountTrain = false;
            _hasSentTrainMoveInput = false;
            _lastSentMoveForward = false;
            _lastSentMoveBackward = false;
            _lastTrainInputSentAt = 0f;
            
            // 初期値として乗車完了済みの場合は即時反映
            // If the player is already riding at the time of entering, reflect that immediately.
            if (context.TryGetContext<InitialRideTrainCarRequest>(out var rideRequest))
            {
                _rideContext = new RidingPlayerStateContext(rideRequest.TargetCarId, rideRequest.SeatIndex);
                _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);
                return;
            }

            // サーバー側に乗車リクエストを送る
            // Send a ride request to the server.
            SendRideRequestAsync().Forget(LogRpcFault);


            #region Internal
            
            async UniTask SendRideRequestAsync()
            {
                if(_cts  != null) return;
                
                var rideRequest = context.GetContext<RideTrainCarRequest>();
                
                _cts  = new CancellationTokenSource();
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(rideRequest.TargetCarId.AsPrimitive());
                var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Ride, target, _cts.Token);
                
                if (response is { Result: RideActionResult.Success })
                {
                    // 乗車を実行
                    // Execute riding.
                    _rideContext = new RidingPlayerStateContext(rideRequest.TargetCarId, response.SeatIndex);
                    _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);
                }
                else
                {
                    // 乗車できなかったのでGameScreenに戻る
                    // Failed to ride, bounce back
                    _isDismountTrain = true;
                }
                
                _cts = null;
            }
                
            // サーバー起因の強制降車を反映
            // Reflect server-forced dismounts.
            void OnRidingStateEventReceived(byte[] payload)
            {
                var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
                if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;

                // 降車とゲームスクリーンへの遷移
                // Dismount and transition to GameScreen.
                if (message.StateType == RidingStateEventType.Dismount)
                {
                    _playerStateController.SetState(PlayerStateEnum.Normal, null);
                    _isDismountTrain = true;
                }
            }

            #endregion
        }

        public UITransitContext GetNextUpdate()
        {
            if (_isDismountTrain) return new UITransitContext(UIStateEnum.GameScreen);

            // まだ乗車が完了していないのであれば何もしない
            // If riding is not yet completed, do nothing.
            if (_rideContext == null) return null;

            // 対象車両が消えたら強制降車
            // Force dismount if the target car has disappeared.
            if (!_trainUnitClientCache.TryGetCarSnapshot( _rideContext.CurrentCarId, out var ridingTrainUnit, out _, out _, out _))
            {
                HideBranchRoutePreview();
                return new UITransitContext(UIStateEnum.GameScreen);
            }
                
            // TrainHUD内部のサブUIステートを実行
            // Run the nested sub-state for the Train HUD.
            _subStateController.Update();
            if (_subStateController.CurrentState != TrainHudScreenUIStateEnum.GameScreen)
            {
                HideBranchRoutePreview();
                return null;
            }

            UpdateBranchRoutePreview(ridingTrainUnit);

            // GameScreenだけ降車処理、列車操作入力を受け付け
            // Only process dismount and train control input on the GameScreen.
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                SendDismountRequestAsync().Forget(LogRpcFault);
            }
            
            var moveForward = UnityEngine.Input.GetKey(KeyCode.W);
            var selectPreviousBranch = UnityEngine.Input.GetKeyDown(KeyCode.A);
            var moveBackward = UnityEngine.Input.GetKey(KeyCode.S);
            var selectNextBranch = UnityEngine.Input.GetKeyDown(KeyCode.D);
            var moveForwardChanged = !_hasSentTrainMoveInput || moveForward != _lastSentMoveForward;
            var moveBackwardChanged = !_hasSentTrainMoveInput || moveBackward != _lastSentMoveBackward;
            var isHeartbeatDue = _hasSentTrainMoveInput && Time.realtimeSinceStartup - _lastTrainInputSentAt >= TrainInputHeartbeatIntervalSeconds;
            var isInput = moveForwardChanged || moveBackwardChanged || isHeartbeatDue || selectPreviousBranch || selectNextBranch;
            if (isInput)
            {
                ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
                    moveForward,
                    moveBackward,
                    selectPreviousBranch,
                    selectNextBranch);
                _hasSentTrainMoveInput = true;
                _lastSentMoveForward = moveForward;
                _lastSentMoveBackward = moveBackward;
                _lastTrainInputSentAt = Time.realtimeSinceStartup;
            }
            

            return null;

            #region Internal

            async UniTask SendDismountRequestAsync()
            {
                if (_cts != null) return;
                
                _cts  = new CancellationTokenSource();
                
                var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Dismount, null, _cts.Token);
                if (response is { Result: RideActionResult.Success })
                {
                    // 降車したので GameScreen へ
                    // Successfully dismounted, transition to GameScreen.
                    _isDismountTrain = true;
                }
                
                _cts = null;
            }
            
            #endregion
        }

        public void OnExit()
        {
            _eventSubscription?.Dispose();
            _eventSubscription = null;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _rideContext = null;

            // 入れ子サブステートを終了（必要に応じてポーズメニューを閉じる）
            // Tear down the nested sub-state (closes the pause menu if it is open).
            _subStateController.ShutdownSubState();

            // ステートを変更して降車処理を実行
            // Change state to trigger dismount processing.
            _playerStateController.SetState(PlayerStateEnum.Normal, null);
            DestroyBranchRoutePreview();

        }

        // fire-and-forget RPC の例外を UnobservedTaskException 経由 log のみに頼らず明示的に拾う。
        // Surface fire-and-forget RPC exceptions explicitly instead of relying on UnobservedTaskException.
        private static void LogRpcFault(Exception exception)
        {
            Debug.LogWarning($"[TrainHUDScreenState] RPC fault: {exception}");
        }

        private void UpdateBranchRoutePreview(ClientTrainUnit trainUnit)
        {
            if (!TryBuildBranchRoutePreviewPoints(trainUnit))
            {
                HideBranchRoutePreview();
                return;
            }

            EnsureBranchRoutePreviewRenderer();
            _branchRoutePreviewObject.SetActive(true);
            _branchRoutePreviewLine.positionCount = _branchRoutePoints.Count;
            for (var i = 0; i < _branchRoutePoints.Count; i++)
            {
                _branchRoutePreviewLine.SetPosition(i, _branchRoutePoints[i]);
            }
        }

        private bool TryBuildBranchRoutePreviewPoints(ClientTrainUnit trainUnit)
        {
            _branchRouteNodes.Clear();
            _branchRoutePoints.Clear();
            var railPosition = trainUnit?.RailPosition;
            if (railPosition == null) return false;

            // 現在の進行先から3node以内で最初の分岐を探す。
            // Search the first branch within three nodes from the current route.
            var previousNode = railPosition.GetNodeJustPassed();
            var currentNode = railPosition.GetNodeApproaching();
            if (currentNode == null) return false;
            var firstSegmentStart = ResolveFirstSegmentStart(railPosition, previousNode, currentNode);
            if (previousNode != null) _branchRouteNodes.Add(previousNode);
            _branchRouteNodes.Add(currentNode);

            if (!TryAppendRouteToNextBranch(previousNode, currentNode, trainUnit.GetManualBranchSelectionIndex())) return false;

            // 現在位置から分岐先まで、レール曲線に沿ったLineRenderer点列を作る。
            // Build LineRenderer points along the rail curve from current position to the selected branch.
            BuildBranchRoutePoints(firstSegmentStart);
            return _branchRoutePoints.Count >= 2;
        }

        private float ResolveFirstSegmentStart(global::Game.Train.RailPositions.RailPosition railPosition, IRailNode previousNode, IRailNode currentNode)
        {
            if (previousNode == null) return 0f;
            var segmentDistance = previousNode.GetDistanceToNode(currentNode);
            if (segmentDistance <= 0) return 0f;
            return Mathf.Clamp01(1f - railPosition.GetDistanceToNextNode() / (float)segmentDistance);
        }

        private bool TryAppendRouteToNextBranch(IRailNode previousNode, IRailNode currentNode, int branchSelectionIndex)
        {
            for (var nodeIndex = 0; nodeIndex < BranchRoutePreviewSearchNodeLimit; nodeIndex++)
            {
                CopyConnectedNodes(currentNode);
                if (_branchCandidateNodes.Count >= 2)
                {
                    var selectedNode = TrainUnitBranchSelector.SelectManualBranchNode(previousNode, currentNode, _branchCandidateNodes, branchSelectionIndex);
                    _branchRouteNodes.Add(selectedNode);
                    return true;
                }

                // 分岐ではない単一路線だけを先へ進む。
                // Continue only through a single non-branch route.
                if (_branchCandidateNodes.Count != 1) return false;
                var nextNode = _branchCandidateNodes[0];
                if (nextNode == null || nextNode == previousNode) return false;
                previousNode = currentNode;
                currentNode = nextNode;
                _branchRouteNodes.Add(currentNode);
            }
            return false;
        }

        private void CopyConnectedNodes(IRailNode node)
        {
            _branchCandidateNodes.Clear();
            foreach (var connectedNode in node.ConnectedNodes)
            {
                _branchCandidateNodes.Add(connectedNode);
            }
        }

        private void BuildBranchRoutePoints(float firstSegmentStart)
        {
            for (var i = 0; i < _branchRouteNodes.Count - 1; i++)
            {
                var startT = i == 0 ? firstSegmentStart : 0f;
                AppendBezierSegmentPoints(_branchRouteNodes[i], _branchRouteNodes[i + 1], startT);
            }
        }

        private void AppendBezierSegmentPoints(IRailNode fromNode, IRailNode toNode, float startT)
        {
            BezierUtility.BuildRenderControlPoints(fromNode.FrontControlPoint, toNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            for (var i = 0; i <= BranchRoutePreviewSamplesPerSegment; i++)
            {
                if (i == 0 && _branchRoutePoints.Count > 0) continue;
                var t = Mathf.Lerp(startT, 1f, i / (float)BranchRoutePreviewSamplesPerSegment);
                _branchRoutePoints.Add(BezierUtility.GetBezierPoint(p0, p1, p2, p3, t) + Vector3.up * BranchRoutePreviewHeightOffset);
            }
        }

        private void EnsureBranchRoutePreviewRenderer()
        {
            if (_branchRoutePreviewLine != null) return;

            // 警告色の赤ではなく、選択ルートとして読める黄橙のワールドラインを使う。
            // Use an amber world-space line so it reads as route selection rather than danger.
            _branchRoutePreviewObject = new GameObject("Train Branch Route Preview");
            _branchRoutePreviewLine = _branchRoutePreviewObject.AddComponent<LineRenderer>();
            _branchRoutePreviewLine.useWorldSpace = true;
            _branchRoutePreviewLine.startWidth = BranchRoutePreviewWidth;
            _branchRoutePreviewLine.endWidth = BranchRoutePreviewWidth;
            _branchRoutePreviewLine.numCornerVertices = 4;
            _branchRoutePreviewLine.numCapVertices = 4;
            _branchRoutePreviewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _branchRoutePreviewLine.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _branchRoutePreviewMaterial = new Material(shader);
            _branchRoutePreviewMaterial.color = BranchRoutePreviewColor;
            _branchRoutePreviewLine.material = _branchRoutePreviewMaterial;
            _branchRoutePreviewLine.startColor = BranchRoutePreviewColor;
            _branchRoutePreviewLine.endColor = BranchRoutePreviewColor;
        }

        private void HideBranchRoutePreview()
        {
            if (_branchRoutePreviewLine == null) return;
            _branchRoutePreviewLine.positionCount = 0;
            _branchRoutePreviewObject.SetActive(false);
        }

        private void DestroyBranchRoutePreview()
        {
            if (_branchRoutePreviewObject != null) UnityEngine.Object.Destroy(_branchRoutePreviewObject);
            if (_branchRoutePreviewMaterial != null) UnityEngine.Object.Destroy(_branchRoutePreviewMaterial);
            _branchRoutePreviewObject = null;
            _branchRoutePreviewLine = null;
            _branchRoutePreviewMaterial = null;
        }
    }
}
