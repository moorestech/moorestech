using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train;
using Client.Input;
using Core.Master;
using Game.Entity.Interface;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : IPlaceSystem
    {
        private readonly ITrainCarPlacementDetector _detector;
        private readonly TrainCarPreviewController _previewController;
        private readonly TrainRailObjectManager _trainRailObjectManager;

        public TrainCarPlaceSystem(ITrainCarPlacementDetector detector, TrainCarPreviewController previewController, TrainRailObjectManager trainRailObjectManager)
        {
            _detector = detector;
            _previewController = previewController;
            _trainRailObjectManager = trainRailObjectManager;
        }
        
        public void Enable()
        {
            _previewController.SetActive(true);
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            var onRailPreview = PlaceTrainOnRail(context);
            var onExistingTrainPreview = PlaceTrainOnExistingTrain(context);
            
            if (!onRailPreview && !onExistingTrainPreview)
            {
                _previewController.SetActive(false);
            }
        }
        
        public void Disable()
        {
            _previewController.SetActive(false);
        }
        
        private bool PlaceTrainOnRail(PlaceSystemUpdateContext context)
        {
            if (!_detector.TryDetectOnRail(context.HoldingItemId, out var hit))
            {
                return false;
            }
            _previewController.SetActive(true);
            _previewController.ShowPreview(context.HoldingItemId, hit.PreviewPosition, hit.PreviewRotation, hit.IsPlaceable);
            
            if (!hit.IsPlaceable)
            {
                return true;
            }
            
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                ClientContext.VanillaApi.SendOnly.PlaceTrainOnRail(hit.Specifier, context.CurrentSelectHotbarSlotIndex);
            }
            
            return true;
        }
        
        private bool PlaceTrainOnExistingTrain(PlaceSystemUpdateContext context)
        {
            if (!_detector.TryDetectOnExistingTrain(out var hit))
            {
                return false;
            }

            _previewController.SetActive(true);
            _previewController.ShowPreview(context.HoldingItemId, hit.PreviewPosition, hit.PreviewRotation, hit.IsPlaceable);

            if (!hit.IsPlaceable)
            {
                return true;
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                var railSpecifiers = BuildRailSpecifiersForNewTrainCar(context.HoldingItemId, hit.Train, hit.IsFront);
                if (railSpecifiers == null)
                {
                    Debug.LogWarning("Failed to build rail specifiers for new train car");
                    return true;
                }

                ClientContext.VanillaApi.SendOnly.PlaceTrainOnExistingTrain(hit.Train.TrainCarId, railSpecifiers, context.CurrentSelectHotbarSlotIndex);
            }

            return true;
        }

        #region Internal

        /// <summary>
        /// 新しい車両を追加するためのRailComponentSpecifier[]を構築
        /// Build RailComponentSpecifier[] for adding a new train car
        /// </summary>
        private RailComponentSpecifier[] BuildRailSpecifiersForNewTrainCar(ItemId holdingItemId, Entity.Object.TrainCarEntityObject targetTrain, bool isFront)
        {
            // 追加する車両の長さを取得
            // Get the length of the train car to add
            if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(holdingItemId, out var trainCarMaster))
            {
                return null;
            }
            var newCarLength = trainCarMaster.Length;

            // 既存列車のRailPositionからスタート地点を決定
            // Determine the start point from the existing train's RailPosition
            var railPosition = targetTrain.RailPosition;
            if (railPosition == null || railPosition.RailNodes == null || railPosition.RailNodes.Count == 0)
            {
                return null;
            }

            var specifiers = TraverseRailsForLength(railPosition, isFront, newCarLength);
            return specifiers;
        }

        /// <summary>
        /// 指定した方向にレールを探索し、必要な長さ分のRailComponentSpecifierを収集
        /// Traverse rails in the specified direction and collect RailComponentSpecifiers for the required length
        /// </summary>
        private RailComponentSpecifier[] TraverseRailsForLength(RailPositionMessagePack railPosition, bool isFront, int requiredLength)
        {
            var result = new List<RailComponentSpecifier>();
            var accumulatedDistance = 0;

            // 開始ノードを決定
            // Determine the starting node
            var railNodes = railPosition.RailNodes;
            RailNodeDataMessagePack currentNode;
            bool traverseForward;

            if (isFront)
            {
                // 前方に追加：RailNodes[0]から進行方向に探索
                // Adding to front: traverse forward from RailNodes[0]
                currentNode = railNodes[0];
                traverseForward = currentNode.IsFrontSide;
            }
            else
            {
                // 後方に追加：RailNodes[last]から逆方向に探索
                // Adding to rear: traverse backward from RailNodes[last]
                currentNode = railNodes[railNodes.Count - 1];
                traverseForward = !currentNode.IsFrontSide;
            }

            // 最初のノードをspecifierに追加
            // Add the first node to specifiers
            result.Add(CreateSpecifierFromNode(currentNode));

            // 必要な長さ分のレールを探索
            // Traverse rails for the required length
            while (accumulatedDistance < requiredLength)
            {
                var connectedRails = _trainRailObjectManager.GetConnectedRails(
                    currentNode.ComponentPosition,
                    currentNode.ComponentId,
                    traverseForward);
                var nextSpline = connectedRails.FirstOrDefault(); //TODO: 適切な選択方法に変更する。例えばプレイヤーに近い方のレールなど

                if (nextSpline == null)
                {
                    // 接続がない場合は探索終了
                    // End traversal if no connection exists
                    break;
                }

                // 次のノードを取得
                // Get the next node
                var connectionData = nextSpline.ConnectionData;
                var fromComponentId = connectionData.FromNode.ComponentId;
                var toComponentId = connectionData.ToNode.ComponentId;
                var distance = connectionData.Distance;

                // 現在のノードがFrom側かTo側かを判定し、次のノードを決定
                // Determine if current node is From or To, and set the next node
                var isCurrentFrom = IsMatchingComponent(currentNode, fromComponentId);
                var nextNode = isCurrentFrom ? connectionData.ToNode : connectionData.FromNode;

                // RailNodeDataMessagePackに変換
                // Convert to RailNodeDataMessagePack
                var nextNodeData = new RailNodeDataMessagePack(
                    (Vector3Int)nextNode.ComponentId.Position,
                    nextNode.ComponentId.ID,
                    nextNode.IsFrontSide,
                    (Vector3)nextNode.ControlPoint.OriginalPosition,
                    (Vector3)nextNode.ControlPoint.ControlPointPosition);

                result.Add(CreateSpecifierFromNode(nextNodeData));
                accumulatedDistance += distance;

                // 次のイテレーションの準備
                // Prepare for the next iteration
                currentNode = nextNodeData;
                // 接続先ノードのIsFrontSideを反転して次の探索方向を決定
                // Invert the IsFrontSide of the connected node to determine the next traversal direction
                traverseForward = !nextNode.IsFrontSide;
            }

            return result.ToArray();
        }

        /// <summary>
        /// RailNodeDataMessagePackからRailComponentSpecifierを作成
        /// Create RailComponentSpecifier from RailNodeDataMessagePack
        /// </summary>
        private static RailComponentSpecifier CreateSpecifierFromNode(RailNodeDataMessagePack node)
        {
            return RailComponentSpecifier.CreateRailSpecifier(node.ComponentPosition);
        }

        /// <summary>
        /// 現在のノードがConnectionDataのコンポーネントと一致するかチェック
        /// Check if the current node matches the component in ConnectionData
        /// </summary>
        private static bool IsMatchingComponent(RailNodeDataMessagePack node, Server.Util.MessagePack.RailComponentIDMessagePack componentId)
        {
            var position = (Vector3Int)componentId.Position;
            return node.ComponentPosition == position && node.ComponentId == componentId.ID;
        }

        #endregion
    }
}