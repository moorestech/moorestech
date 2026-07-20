using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.Game.InGame.UnlockState
{
    public class ClientGameUnlockStateData : IGameUnlockStateData
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _itemUnlockStateInfos;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos => _challengeCategoryUnlockStateInfos;
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos => _machineRecipeUnlockStateInfos;

        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos => _blockUnlockStateInfos;
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos => _trainCarUnlockStateInfos;
        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos => _connectToolUnlockStateInfos;

        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        private readonly Dictionary<ItemId, ItemUnlockStateInfo> _itemUnlockStateInfos = new();
        private readonly Dictionary<Guid, ChallengeCategoryUnlockStateInfo> _challengeCategoryUnlockStateInfos = new();
        private readonly Dictionary<Guid, MachineRecipeUnlockStateInfo> _machineRecipeUnlockStateInfos = new();
        private readonly Dictionary<Guid, BlockUnlockStateInfo> _blockUnlockStateInfos = new();
        private readonly Dictionary<Guid, TrainCarUnlockStateInfo> _trainCarUnlockStateInfos = new();
        private readonly Dictionary<Guid, ConnectToolUnlockStateInfo> _connectToolUnlockStateInfos = new();
        
        public ClientGameUnlockStateData(InitialHandshakeResponse initialHandshakeResponse)
        {
            var unlockState = initialHandshakeResponse.UnlockState;
            foreach (var lockedGuid in unlockState.LockedCraftRecipeGuids)
            {
                _recipeUnlockStateInfos[lockedGuid] = new CraftRecipeUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedCraftRecipeGuids)
            {
                _recipeUnlockStateInfos[unlockedGuid] = new CraftRecipeUnlockStateInfo(unlockedGuid, true);
            }
            
            foreach (var lockedItemId in unlockState.LockedItemIds)
            {
                _itemUnlockStateInfos[lockedItemId] = new ItemUnlockStateInfo(lockedItemId, false);
            }
            foreach (var unlockedItemId in unlockState.UnlockedItemIds)
            {
                _itemUnlockStateInfos[unlockedItemId] = new ItemUnlockStateInfo(unlockedItemId, true);
            }
            
            foreach (var lockedChallengeId in unlockState.LockedChallengeCategoryGuids)
            {
                _challengeCategoryUnlockStateInfos[lockedChallengeId] = new ChallengeCategoryUnlockStateInfo(lockedChallengeId, false);
            }
            foreach (var unlockedChallengeId in unlockState.UnlockedChallengeCategoryGuids)
            {
                _challengeCategoryUnlockStateInfos[unlockedChallengeId] = new ChallengeCategoryUnlockStateInfo(unlockedChallengeId, true);
            }

            // 機械レシピのアンロック状態を初期化
            // Initialize machine recipe unlock states
            foreach (var lockedGuid in unlockState.LockedMachineRecipeGuids)
            {
                _machineRecipeUnlockStateInfos[lockedGuid] = new MachineRecipeUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedMachineRecipeGuids)
            {
                _machineRecipeUnlockStateInfos[unlockedGuid] = new MachineRecipeUnlockStateInfo(unlockedGuid, true);
            }

            // ブロックの解放状態を初期化
            // Initialize block unlock states
            foreach (var lockedGuid in unlockState.LockedBlockGuids)
            {
                _blockUnlockStateInfos[lockedGuid] = new BlockUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedBlockGuids)
            {
                _blockUnlockStateInfos[unlockedGuid] = new BlockUnlockStateInfo(unlockedGuid, true);
            }

            // 列車車両の解放状態を初期化
            // Initialize train car unlock states
            foreach (var lockedGuid in unlockState.LockedTrainCarGuids)
            {
                _trainCarUnlockStateInfos[lockedGuid] = new TrainCarUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedTrainCarGuids)
            {
                _trainCarUnlockStateInfos[unlockedGuid] = new TrainCarUnlockStateInfo(unlockedGuid, true);
            }

            // 接続ツールの解放状態を初期化
            // Initialize connect tool unlock states
            foreach (var lockedGuid in unlockState.LockedConnectToolGuids)
            {
                _connectToolUnlockStateInfos[lockedGuid] = new ConnectToolUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedConnectToolGuids)
            {
                _connectToolUnlockStateInfos[unlockedGuid] = new ConnectToolUnlockStateInfo(unlockedGuid, true);
            }

            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, OnUpdateUnlock);
        }
        
        public void OnUpdateUnlock(byte[] payload)
        {
             var message = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(payload);
             
             switch (message.UnlockEventType)
             {
                 case UnlockEventType.CraftRecipe:
                     var recipeGuid = message.UnlockedCraftRecipeGuid;
                     _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeGuid, true);
                     break;
                 case UnlockEventType.Item:
                     var itemId = message.UnlockedItemId;
                     _itemUnlockStateInfos[itemId] = new ItemUnlockStateInfo(itemId, true);
                     break;
                 case UnlockEventType.ChallengeCategory:
                     var challengeId = message.UnlockedChallengeCategoryGuid;
                     _challengeCategoryUnlockStateInfos[challengeId] = new ChallengeCategoryUnlockStateInfo(challengeId, true);
                     break;
                 case UnlockEventType.MachineRecipe:
                     var machineRecipeGuid = message.UnlockedMachineRecipeGuid;
                     _machineRecipeUnlockStateInfos[machineRecipeGuid] = new MachineRecipeUnlockStateInfo(machineRecipeGuid, true);
                     break;
                 // ブロックの解放をイベントから反映する
                 // Reflect block unlock from the event
                 case UnlockEventType.Block:
                     var blockGuid = message.UnlockedBlockGuid;
                     _blockUnlockStateInfos[blockGuid] = new BlockUnlockStateInfo(blockGuid, true);
                     break;
                 // 列車車両の解放をイベントから反映する
                 // Reflect train car unlock from the event
                 case UnlockEventType.TrainCar:
                     var trainCarGuid = message.UnlockedTrainCarGuid;
                     _trainCarUnlockStateInfos[trainCarGuid] = new TrainCarUnlockStateInfo(trainCarGuid, true);
                     break;
                 // 接続ツールの解放をイベントから反映する
                 // Reflect connect tool unlock from the event
                 case UnlockEventType.ConnectTool:
                     var connectToolGuid = message.UnlockedConnectToolGuid;
                     _connectToolUnlockStateInfos[connectToolGuid] = new ConnectToolUnlockStateInfo(connectToolGuid, true);
                     break;
                 default:
                     throw new ArgumentOutOfRangeException();
             }
        }
    }
}