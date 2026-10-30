using System;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Research;
using UnityEngine;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    // 搬送網の構築と素材投入をtick末尾でまとめる
    // Construct the feed network and insert its source together at the server tick end
    public sealed class MachineInputStallFeedSetupStep : ServerTickEndTestStep
    {
        private readonly Vector3Int _machinePosition;
        private readonly ItemId _sourceItemId;

        public IBlock Chest { get; private set; }
        public IBlock Belt { get; private set; }
        public int RemainingSourceCount { get; private set; }

        public MachineInputStallFeedSetupStep(Vector3Int machinePosition, ItemId sourceItemId)
        {
            _machinePosition = machinePosition;
            _sourceItemId = sourceItemId;
        }

        protected override void Execute()
        {
            // 網再構築と配置のdirty更新を競合させない
            // Keep placement dirty flags from racing the transport network rebuild
            Chest = PlaceBlock("量子チェスト", _machinePosition + Vector3Int.back * 2, BlockDirection.North);
            Belt = PlaceBlock("直進高速ベルトコンベア", _machinePosition + Vector3Int.back, BlockDirection.North);
            if (Chest == null || Belt == null)
            {
                Debug.LogError($"[MachineInputStallFeedSetupStep] 搬送元の配置に失敗しました machine:{_machinePosition} chest:{Chest != null} belt:{Belt != null}");
                return;
            }

            // 実際のチェストからベルトへ流す素材を積む
            // Stock the real chest so the belt delivers the source material
            RemainingSourceCount = InsertItemToBlock(Chest, _sourceItemId, 1).Count;
        }
    }

    // 研究による上限変更もゲーム更新と同じスレッドで行う
    // Apply the research cap change on the same thread as the game update
    public sealed class MachineInputStallResearchStep : ServerTickEndTestStep
    {
        private readonly Guid _researchGuid;
        private readonly int _playerId;

        public bool Completed { get; private set; }

        public MachineInputStallResearchStep(Guid researchGuid, int playerId)
        {
            _researchGuid = researchGuid;
            _playerId = playerId;
        }

        protected override void Execute()
        {
            Completed = ServerContext.GetService<IResearchDataStore>().CompleteResearch(_researchGuid, _playerId);
        }
    }
}
