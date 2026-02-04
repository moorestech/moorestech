using Core.Item.Interface;
using Core.Master;
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
        public string StationName { get; }
        // ブロックパラメータ参照を保持
        // Keep reference to the generated train station parameter
        private readonly TrainStationBlockParam _param;
        public Guid? _dockedTrainId;
        private Guid? _dockedCarId;
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
            _armAnimationTicks = _param.LoadingSpeed;
        }
        public bool CanDock(ITrainDockHandle handle)
        {
            if (handle == null) return false;
            if (!_dockedTrainId.HasValue && !_dockedCarId.HasValue) return true;
            return _dockedTrainId == handle.TrainId && _dockedCarId == handle.CarId;
        }

        public void OnTrainDocked(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId.HasValue && _dockedTrainId != handle.TrainId) return;
            if (_dockedCarId.HasValue && _dockedCarId != handle.CarId) return;
            _dockedTrainId = handle.TrainId;
            _dockedCarId = handle.CarId;
            _dockedHandle = handle as TrainDockHandle;
            _shouldStartOnDock = true;
            UpdateDockedReferences(handle);
        }

        public void OnTrainDockedTick(ITrainDockHandle handle)
        {
            if (handle == null) return;
            if (_dockedTrainId != handle.TrainId || _dockedCarId != handle.CarId) return;
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
            var isDocked = _dockedTrainId.HasValue && _dockedCarId.HasValue;
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
                _armProgressTicks = Math.Min(1, _armAnimationTicks);
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
                if (_dockedTrainCar.IsInventoryFull()) return false;
                for (var slot = 0; slot < _dockedStationInventory.GetSlotSize(); slot++)
                {
                    var stack = _dockedStationInventory.GetItem(slot);
                    if (!IsEmptyStack(stack)) return true;
                }

                return false;
            }

            void TransferItemsToTrainCar()
            {
                for (var slot = 0; slot < _dockedStationInventory.GetSlotSize(); slot++)
                {
                    var slotStack = _dockedStationInventory.GetItem(slot);
                    if (IsEmptyStack(slotStack)) continue;

                    var remainder = _dockedTrainCar.InsertItem(slotStack);
                    if (IsSameStack(slotStack, remainder)) continue;

                    _dockedStationInventory.SetItem(slot, remainder);
                    if (_dockedTrainCar.IsInventoryFull()) break;
                }
            }

            bool IsEmptyStack(IItemStack stack) => stack == null || stack.Id == ItemMaster.EmptyItemId || stack.Count == 0;

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
            _dockedCarId = null;
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
            if (_dockedTrainId == handle.TrainId && _dockedCarId == handle.CarId)
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
            var stationComponentSaverData = new StationComponentSaverData(StationName);
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
            public StationComponentSaverData(string name)
            {
                itemJson = new List<ItemStackSaveJsonObject>();
                stationName = name;
            }
        }




        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
