using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Train.Unit;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// 駅(TrainStation)用のコンポーネント。
    /// オープン可能なインベントリを持ち、かつ列車が到着・出発した状態も持つ。
    /// </summary>
    public class StationComponent : IBlockSaveState, ITrainDockingReceiver, IUpdatableBlockComponent
    {
        public string StationName { get; private set; }
        // ブロックパラメータ参照を保持
        // Keep reference to the generated train station parameter
        private readonly TrainStationBlockParam _param;
        public Guid? _dockedTrainId;
        private long? _dockedTrainCarInstanceId;
        private TrainCar _dockedTrainCar;
        private IBlockInventory _dockedStationInventory;
        private TrainDockHandle _dockedHandle;
        // アームアニメーションのtick設定
        // Arm animation tick settings
        private readonly int _armAnimationTicks;

        private enum ArmState
        {
            Idle,
            Extending,
            Retracting
        }

        private ArmState _armState = ArmState.Idle;
        private int _armProgressTicks;
        private bool _shouldStartOnDock;
        // インベントリスロット数やUI更新のための設定
        // マスター定義のスロット容量を公開
        // Expose inventory slot capacity defined in the block parameter
        public int InventorySlotCount => _param.ItemSlotCount;
        public bool IsDestroy { get; private set; }

        public StationComponent(string stationName, TrainStationBlockParam param)
        {
            StationName = stationName;
            _param = param;
            var armAnimationTicks = GameUpdater.SecondsToTicks(_param.LoadingAnimeSpeed);
            _armAnimationTicks = armAnimationTicks > int.MaxValue ? int.MaxValue : (int)armAnimationTicks;
        }

        public StationComponent(Dictionary<string, string> componentStates, TrainStationBlockParam param) : this("test", param)
        {
            var serialized = componentStates[SaveKey];
            var saveData = JsonConvert.DeserializeObject<StationComponentSaverData>(serialized);
            if (saveData == null) return;
            StationName = saveData.stationName;
            _armState = (ArmState)saveData.armState;
            _armProgressTicks = Math.Min(Math.Max(0, saveData.armProgressTicks), _armAnimationTicks);
            _shouldStartOnDock = saveData.shouldStartOnDock;
        }
        public bool CanDock(ITrainDockHandle handle)
        {
            if (handle == null) return false;
            if (!_dockedTrainId.HasValue && !_dockedTrainCarInstanceId.HasValue) return true;
            return _dockedTrainId == handle.TrainId && _dockedTrainCarInstanceId == handle.TrainCarInstanceId;
        }

        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId.HasValue && _dockedTrainId != handle.TrainId) return;
            if (_dockedTrainCarInstanceId.HasValue && _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            _dockedTrainId = handle.TrainId;
            _dockedTrainCarInstanceId = handle.TrainCarInstanceId;
            _dockedHandle = handle as TrainDockHandle;
            if (_armState == ArmState.Idle && _armProgressTicks == 0) _shouldStartOnDock = true;
            UpdateDockedReferences(handle);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId != handle.TrainId || _dockedTrainCarInstanceId != handle.TrainCarInstanceId) return;
            _dockedHandle = handle as TrainDockHandle;
            if (_dockedTrainCar != null && _dockedStationInventory != null) return;
            UpdateDockedReferences(handle);
        }

        public void Update()
        {
            // アームアニメーションと転送タイミングを1tick進める
            // Advance arm animation and transfer timing per tick
            if (IsDestroy) return;

            // ドッキング状態に応じて参照を解決する
            // Resolve docking references when needed
            var isDocked = _dockedTrainId.HasValue && _dockedTrainCarInstanceId.HasValue;
            if (isDocked && _dockedHandle != null && (_dockedTrainCar == null || _dockedStationInventory == null)) UpdateDockedReferences(_dockedHandle);

            // アーム状態を進めて接触タイミングで一括転送する
            // Advance arm state and perform bulk transfer on contact timing
            switch (_armState)
            {
                case ArmState.Idle:
                    if (isDocked && (_shouldStartOnDock || CanTransferNow())) StartExtending();
                    _shouldStartOnDock = false;
                    break;

                case ArmState.Extending:
                    if (!isDocked)
                    {
                        StartRetractingFromCurrent();
                        break;
                    }

                    if (_armProgressTicks < _armAnimationTicks)
                    {
                        _armProgressTicks++;
                        break;
                    }

                    if (CanTransferNow()) TransferItemsToTrainCar();
                    StartRetractingFromFull();
                    break;

                case ArmState.Retracting:
                    if (_armProgressTicks > 0)
                    {
                        _armProgressTicks--;
                        if (_armProgressTicks == 0) _armState = ArmState.Idle;
                        break;
                    }

                    _armState = ArmState.Idle;
                    break;
            }

            #region Internal

            void StartExtending()
            {
                _armState = ArmState.Extending;
                _armProgressTicks = 1;
            }

            void StartRetractingFromFull()
            {
                _armState = ArmState.Retracting;
                _armProgressTicks = _armAnimationTicks;
            }

            bool CanTransferNow()
            {
                if (!isDocked) return false;
                if (_dockedTrainCar == null || _dockedStationInventory == null) return false;
                return HasTransferCandidate(
                    _dockedStationInventory.GetSlotSize(),
                    _dockedStationInventory.GetItem,
                    _dockedTrainCar.GetSlotSize(),
                    _dockedTrainCar.GetItem);
            }

            void TransferItemsToTrainCar()
            {
                TransferByDestinationPriority(
                    _dockedStationInventory.GetSlotSize(),
                    _dockedStationInventory.GetItem,
                    _dockedStationInventory.SetItem,
                    _dockedTrainCar.GetSlotSize(),
                    _dockedTrainCar.GetItem,
                    _dockedTrainCar.SetItem);
            }

            bool IsEmptyStack(IItemStack stack) => stack == null || stack.Id == ItemMaster.EmptyItemId || stack.Count == 0;

            bool HasTransferCandidate(int sourceSlotCount, Func<int, IItemStack> getSource, int destinationSlotCount, Func<int, IItemStack> getDestination)
            {
                for (var destinationSlot = 0; destinationSlot < destinationSlotCount; destinationSlot++)
                {
                    var destination = getDestination(destinationSlot);
                    if (destination == null) continue;

                    for (var sourceSlot = 0; sourceSlot < sourceSlotCount; sourceSlot++)
                    {
                        var source = getSource(sourceSlot);
                        if (IsEmptyStack(source)) continue;
                        if (destination.IsAllowedToAddWithRemain(source)) return true;
                    }
                }

                return false;
            }

            void TransferByDestinationPriority(int sourceSlotCount, Func<int, IItemStack> getSource, Action<int, IItemStack> setSource, int destinationSlotCount, Func<int, IItemStack> getDestination, Action<int, IItemStack> setDestination)
            {
                for (var destinationSlot = 0; destinationSlot < destinationSlotCount; destinationSlot++)
                {
                    var destination = getDestination(destinationSlot);
                    if (destination == null) continue;

                    for (var sourceSlot = 0; sourceSlot < sourceSlotCount; sourceSlot++)
                    {
                        var source = getSource(sourceSlot);
                        if (IsEmptyStack(source)) continue;
                        if (!destination.IsAllowedToAddWithRemain(source)) continue;

                        var processResult = destination.AddItem(source);
                        if (IsSameStack(destination, processResult.ProcessResultItemStack) && IsSameStack(source, processResult.RemainderItemStack)) continue;

                        setDestination(destinationSlot, processResult.ProcessResultItemStack);
                        setSource(sourceSlot, processResult.RemainderItemStack);
                        destination = processResult.ProcessResultItemStack;
                        if (IsDestinationFull(destination)) break;
                    }
                }
            }

            bool IsDestinationFull(IItemStack destinationStack)
            {
                if (IsEmptyStack(destinationStack)) return false;
                return destinationStack.Count >= MasterHolder.ItemMaster.GetItemMaster(destinationStack.Id).MaxStack;
            }

            #endregion
        }

        private static bool IsSameStack(IItemStack left, IItemStack right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.Id == right.Id && left.Count == right.Count;
        }

        private void UpdateDockedReferences(ITrainDockHandle handle)
        {
            if (handle is not TrainDockHandle dockHandle)
            {
                _dockedTrainCar = null;
                _dockedStationInventory = null;
                return;
            }

            _dockedHandle = dockHandle;
            _dockedTrainCar = dockHandle.TrainCar;
            _dockedStationInventory = ResolveStationInventory(_dockedTrainCar);
        }

        private void ClearDockedReferences()
        {
            _dockedTrainId = null;
            _dockedTrainCarInstanceId = null;
            _dockedHandle = null;
            _shouldStartOnDock = false;
            _dockedTrainCar = null;
            _dockedStationInventory = null;
        }

        private void StartRetractingFromCurrent()
        {
            // 現在の進捗からリトラクトへ移行する
            // Switch to retracting from the current arm progress
            _armState = ArmState.Retracting;
            _armProgressTicks = Math.Min(_armProgressTicks, _armAnimationTicks);
        }

        private IBlockInventory ResolveStationInventory(TrainCar trainCar)
        {
            if (trainCar?.dockingblock == null)
            {
                return null;
            }

            return trainCar.dockingblock.ComponentManager.TryGetComponent<IBlockInventory>(out var inventory)
                ? inventory
                : null;
        }

        public void OnTrainUndocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId == handle.TrainId && _dockedTrainCarInstanceId == handle.TrainCarInstanceId)
            {
                ClearDockedReferences();
                if (_armState == ArmState.Extending) StartRetractingFromCurrent();
            }
        }

        public void ForceUndock()
        {
            ClearDockedReferences();
            if (_armState == ArmState.Extending) StartRetractingFromCurrent();
        }

        /// <summary>
        /// セーブ機能：ブロックが破壊されたりサーバーを落とすとき用
        /// </summary>
        public string SaveKey { get; } = typeof(StationComponent).FullName;


        public string GetSaveState()
        {
            var stationComponentSaverData = new StationComponentSaverData(StationName, (int)_armState, _armProgressTicks, _shouldStartOnDock);
            /*foreach (var item in _itemDataStoreService.InventoryItems)
            {
                stationComponentSaverData.itemJson.Add(new ItemStackSaveJsonObject(item));
            }*/
            return JsonConvert.SerializeObject(stationComponentSaverData);
        }

        [Serializable]
        public class StationComponentSaverData
        {
            public List<ItemStackSaveJsonObject> itemJson;
            public string stationName;
            public int armState;
            public int armProgressTicks;
            public bool shouldStartOnDock;

            public StationComponentSaverData(string name, int state, int progressTicks, bool startOnDock)
            {
                itemJson = new List<ItemStackSaveJsonObject>();
                stationName = name;
                armState = state;
                armProgressTicks = progressTicks;
                shouldStartOnDock = startOnDock;
            }
        }




        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
