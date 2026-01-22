# Server Train/Rail asmdef and folder plan (draft)

Scope
- Server only (moorestech_server)
- Train and rail domain
- Plan only, no implementation changes

Principles
- Rail is Train-only
- asmdef name == folder name == root namespace
- Keep Core.Master and other global systems in place unless listed
- Avoid new dependency cycles

Target folders and asmdef names (new)
- Assets/Scripts/Game.Train/RailGraph (Game.Train.RailGraph.asmdef)
- Assets/Scripts/Game.Train/RailGraph/Notification (Game.Train.RailGraph.Notification.asmdef)
- Assets/Scripts/Game.Train/RailGraph/Utility (Game.Train.RailGraph.Utility.asmdef)
- Assets/Scripts/Game.Train/RailPosition (Game.Train.RailPosition.asmdef, shared with client candidate)
- Assets/Scripts/Game.Train/RailCalc (Game.Train.RailCalc.asmdef)
- Assets/Scripts/Game.Train/Unit (Game.Train.Unit.asmdef)
- Assets/Scripts/Game.Train/Diagram (Game.Train.Diagram.asmdef)
- Assets/Scripts/Game.Train/SaveLoad (Game.Train.SaveLoad.asmdef)
- Assets/Scripts/Game.Train/Event (Game.Train.Event.asmdef, train domain events including inventory and non-item events)

Move map (source -> target)
- Notes:
  - Entries with identical source/target paths are informational only (no move, no new folder creation).
  - Only create new folders explicitly listed (e.g., RailGraph/Notification, RailGraph/Utility).

Game.Train.RailGraph.Notification
- Assets/Scripts/Game.Train/RailGraph/RailConnectionInitializationNotifier.cs -> Assets/Scripts/Game.Train/RailGraph/Notification/RailConnectionInitializationNotifier.cs
- Assets/Scripts/Game.Train/RailGraph/RailConnectionRemovalNotifier.cs -> Assets/Scripts/Game.Train/RailGraph/Notification/RailConnectionRemovalNotifier.cs
- Assets/Scripts/Game.Train/RailGraph/RailNodeInitializationNotifier.cs -> Assets/Scripts/Game.Train/RailGraph/Notification/RailNodeInitializationNotifier.cs
- Assets/Scripts/Game.Train/RailGraph/RailNodeRemovalNotifier.cs -> Assets/Scripts/Game.Train/RailGraph/Notification/RailNodeRemovalNotifier.cs

Game.Train.RailGraph
- Assets/Scripts/Game.Train/RailGraph/IRailGraphDatastore.cs -> Assets/Scripts/Game.Train/RailGraph/IRailGraphDatastore.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/IRailGraphProvider.cs -> Assets/Scripts/Game.Train/RailGraph/IRailGraphProvider.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/IRailGraphTraversalProvider.cs -> Assets/Scripts/Game.Train/RailGraph/IRailGraphTraversalProvider.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/IRailNode.cs -> Assets/Scripts/Game.Train/RailGraph/IRailNode.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailConnectionCommandHandler.cs -> Assets/Scripts/Game.Train/RailGraph/RailConnectionCommandHandler.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailControlPoint.cs -> Assets/Scripts/Game.Train/RailGraph/RailControlPoint.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailGraphDatastore.cs -> Assets/Scripts/Game.Train/RailGraph/RailGraphDatastore.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailGraphHashCalculator.cs -> Assets/Scripts/Game.Train/RailGraph/RailGraphHashCalculator.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailGraphPathFinder.cs -> Assets/Scripts/Game.Train/RailGraph/RailGraphPathFinder.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailGraphSnapshot.cs -> Assets/Scripts/Game.Train/RailGraph/RailGraphSnapshot.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailNode.cs -> Assets/Scripts/Game.Train/RailGraph/RailNode.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailNodeIdAllocator.cs -> Assets/Scripts/Game.Train/RailGraph/RailNodeIdAllocator.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/RailPathTracer.cs -> Assets/Scripts/Game.Train/RailGraph/RailPathTracer.cs (no move)
- Assets/Scripts/Game.Train/RailGraph/StationReference.cs -> Assets/Scripts/Game.Train/RailGraph/StationReference.cs (no move)

Game.Train.RailGraph.Utility
- Assets/Scripts/Game.Train/RailGraph/MinHeap.cs -> Assets/Scripts/Game.Train/RailGraph/Utility/MinHeap.cs

Game.Train.RailCalc
- Assets/Scripts/Game.Train/Utility/RailNodeCalculate.cs -> Assets/Scripts/Game.Train/RailCalc/RailNodeCalculate.cs
- Assets/Scripts/Game.Train/Utility/BezierUtility.cs -> Assets/Scripts/Game.Train/RailCalc/BezierUtility.cs

Game.Train.RailPosition
- Assets/Scripts/Game.Train/RailGraph/RailPosition.cs -> Assets/Scripts/Game.Train/RailPosition/RailPosition.cs
- Assets/Scripts/Game.Train/RailGraph/RailPositionFactory.cs -> Assets/Scripts/Game.Train/RailPosition/RailPositionFactory.cs
- Assets/Scripts/Game.Train/Common/TrainRailPositionManager.cs -> Assets/Scripts/Game.Train/RailPosition/TrainRailPositionManager.cs

Game.Train.Unit
- Assets/Scripts/Game.Train/Train/TrainCar.cs -> Assets/Scripts/Game.Train/Unit/TrainCar.cs
- Assets/Scripts/Game.Train/Train/TrainMotionSimulation.cs -> Assets/Scripts/Game.Train/Unit/TrainMotionSimulation.cs
- Assets/Scripts/Game.Train/Train/TrainSnapshots.cs -> Assets/Scripts/Game.Train/Unit/TrainSnapshots.cs
- Assets/Scripts/Game.Train/Train/TrainUnit.cs -> Assets/Scripts/Game.Train/Unit/TrainUnit.cs
- Assets/Scripts/Game.Train/Train/TrainUnitSnapshotFactory.cs -> Assets/Scripts/Game.Train/Unit/TrainUnitSnapshotFactory.cs
- Assets/Scripts/Game.Train/Train/TrainUnitSnapshotHashCalculator.cs -> Assets/Scripts/Game.Train/Unit/TrainUnitSnapshotHashCalculator.cs
- Assets/Scripts/Game.Train/Common/TrainUpdateService.cs -> Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs
- Assets/Scripts/Game.Train/Common/TrainUnitInitializationNotifier.cs -> Assets/Scripts/Game.Train/Unit/TrainUnitInitializationNotifier.cs
- Assets/Scripts/Game.Train/Train/ITrainUnitStationDockingListener.cs -> Assets/Scripts/Game.Train/Unit/ITrainUnitStationDockingListener.cs
- Assets/Scripts/Game.Train/Train/TrainUnitStationDocking.cs -> Assets/Scripts/Game.Train/Unit/TrainUnitStationDocking.cs
- Assets/Scripts/Game.Train/Common/TrainDockHandle.cs -> Assets/Scripts/Game.Train/Unit/TrainDockHandle.cs
- Assets/Scripts/Game.Train/Common/TrainDockingStateRestorer.cs -> Assets/Scripts/Game.Train/Unit/TrainDockingStateRestorer.cs
- Assets/Scripts/Game.Train/Utility/TrainLengthConverter.cs -> Assets/Scripts/Game.Train/Unit/TrainLengthConverter.cs

Game.Train.Diagram
- Assets/Scripts/Game.Train/Train/ITrainDiagramContext.cs -> Assets/Scripts/Game.Train/Diagram/ITrainDiagramContext.cs
- Assets/Scripts/Game.Train/Train/TrainDiagram.cs -> Assets/Scripts/Game.Train/Diagram/TrainDiagram.cs
- Assets/Scripts/Game.Train/Train/TrainDiagramEntry.cs -> Assets/Scripts/Game.Train/Diagram/TrainDiagramEntry.cs
- Assets/Scripts/Game.Train/Train/TrainDiagramHashCalculator.cs -> Assets/Scripts/Game.Train/Diagram/TrainDiagramHashCalculator.cs
- Assets/Scripts/Game.Train/Common/TrainDiagramManager.cs -> Assets/Scripts/Game.Train/Diagram/TrainDiagramManager.cs

Game.Train.SaveLoad
- Assets/Scripts/Game.Train/Common/TrainSaveLoadService.cs -> Assets/Scripts/Game.Train/SaveLoad/TrainSaveLoadService.cs
- Assets/Scripts/Game.Train/RailGraph/RailSaverData.cs -> Assets/Scripts/Game.Train/SaveLoad/RailSaverData.cs

Game.Train.Event
- Assets/Scripts/Game.Train/Event/ITrainUpdateEvent.cs -> Assets/Scripts/Game.Train/Event/ITrainUpdateEvent.cs
- Assets/Scripts/Game.Train/Event/TrainUpdateEvent.cs -> Assets/Scripts/Game.Train/Event/TrainUpdateEvent.cs
- Assets/Scripts/Game.Train/Event/TrainInventoryUpdateEventProperties.cs -> Assets/Scripts/Game.Train/Event/TrainInventoryUpdateEventProperties.cs

Notes
- Game.Train/Entity folder is currently empty (optional cleanup later).
- Train block components stay under Game.Block (no Game.Block.Train asmdef); adjust move map targets when executing.
- Protocol implementations stay under Server.Protocol (no Server.Protocol.Train asmdef); keep current layout for consistency.
- Server.Event stays under existing Server.Event (no Server.Event.Train asmdef); keep event packets colocated with other domains.
- MessagePack serializers stay under Server.Util.MessagePack (no Server.Util.MessagePack.Train asmdef); keep shared utilities together.
- RailGraph is graph structure and persistence (includes traversal/path tracing); RailGraph.Notification is graph change events; RailPosition is shared position state; RailCalc is geometry/position math.
- Unit also contains update/runtime and docking to avoid Unit <-> Runtime/Docking circular dependencies.
- Dependency memo: RailGraph.Notification -> RailGraph; Unit -> Diagram, RailGraph, RailGraph.Notification, RailPosition, RailCalc; Diagram -> RailGraph; RailPosition -> RailGraph; SaveLoad -> Unit; Event -> Unit.
- RailGraph.Utility is for graph-agnostic utilities (no RailGraph type dependencies).
- Dependencies need validation after moves; adjust asmdef references by compile errors, then iterate.

Client Train/Rail plan (draft)

Scope
- Client only (moorestech_client)
- Train and rail domain
- Plan only, no implementation changes

Principles
- Rail is Train-only
- asmdef name == folder name == root namespace
- Avoid new dependency cycles

Target folders and asmdef names (new)
- Assets/Scripts/Client.Game/InGame/Train/RailGraph (Client.Game.InGame.Train.RailGraph.asmdef)
- Assets/Scripts/Client.Game/InGame/Train/Unit (Client.Game.InGame.Train.Unit.asmdef)
- Assets/Scripts/Client.Game/InGame/Train/Diagram (Client.Game.InGame.Train.Diagram.asmdef)
- Assets/Scripts/Client.Game/InGame/Train/Network (Client.Game.InGame.Train.Network.asmdef)
- Assets/Scripts/Client.Game/InGame/Train/View (Client.Game.InGame.Train.View.asmdef)

Move map (source -> target)
- Notes:
  - Entries with identical source/target paths are informational only (no move, no new folder creation).
  - Only create new folders explicitly listed (e.g., Train/RailGraph, Train/Unit).

Client.Game.InGame.Train.RailGraph
- Assets/Scripts/Client.Game/InGame/Train/BezierRailChain.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/BezierRailChain.cs
- Assets/Scripts/Client.Game/InGame/Train/BezierRailMesh.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/BezierRailMesh.cs
- Assets/Scripts/Client.Game/InGame/Train/ClientRailNode.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/ClientRailNode.cs
- Assets/Scripts/Client.Game/InGame/Train/ClientStationReferenceRegistry.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/ClientStationReferenceRegistry.cs
- Assets/Scripts/Client.Game/InGame/Train/DeleteTargetRail.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/DeleteTargetRail.cs
- Assets/Scripts/Client.Game/InGame/Train/RailGraphClientCache.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailGraphClientCache.cs
- Assets/Scripts/Client.Game/InGame/Train/RailGraphHashVerifier.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailGraphHashVerifier.cs
- Assets/Scripts/Client.Game/InGame/Train/RailObjectIdCarrier.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/RailObjectIdCarrier.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainRailObjectManager.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs
- Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/TrainRailStateChangeProcessor.cs -> Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailStateChangeProcessor.cs

Client.Game.InGame.Train.Unit
- Assets/Scripts/Client.Game/InGame/Train/ClientTrainUnit.cs -> Assets/Scripts/Client.Game/InGame/Train/Unit/ClientTrainUnit.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainUnitClientCache.cs -> Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientCache.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainUnitClientSimulator.cs -> Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainUnitSnapshotHashCalculator.cs -> Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitSnapshotHashCalculator.cs

Client.Game.InGame.Train.Diagram
- Assets/Scripts/Client.Game/InGame/Train/ClientTrainDiagram.cs -> Assets/Scripts/Client.Game/InGame/Train/Diagram/ClientTrainDiagram.cs

Client.Game.InGame.Train.Network
- Assets/Scripts/Client.Game/InGame/Train/RailGraphCacheNetworkHandler.cs -> Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphCacheNetworkHandler.cs
- Assets/Scripts/Client.Game/InGame/Train/RailGraphConnectionNetworkHandler.cs -> Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphConnectionNetworkHandler.cs
- Assets/Scripts/Client.Game/InGame/Train/RailGraphMessagePackExtensions.cs -> Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphMessagePackExtensions.cs
- Assets/Scripts/Client.Game/InGame/Train/RailGraphSnapshotApplier.cs -> Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphSnapshotApplier.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainDiagramEventNetworkHandler.cs -> Assets/Scripts/Client.Game/InGame/Train/Network/TrainDiagramEventNetworkHandler.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainUnitCreatedEventNetworkHandler.cs -> Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitCreatedEventNetworkHandler.cs

Client.Game.InGame.Train.View
- Assets/Scripts/Client.Game/InGame/Train/TrainCarPoseCalculator.cs -> Assets/Scripts/Client.Game/InGame/Train/View/TrainCarPoseCalculator.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainCarEntityPoseUpdater.cs -> Assets/Scripts/Client.Game/InGame/Train/View/TrainCarEntityPoseUpdater.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainUnitHashVerifier.cs -> Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitHashVerifier.cs
- Assets/Scripts/Client.Game/InGame/Train/TrainUnitSnapshotApplier.cs -> Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitSnapshotApplier.cs

Notes
- Move list is based on file names and current folder placement; adjust based on actual dependencies after compile errors.
