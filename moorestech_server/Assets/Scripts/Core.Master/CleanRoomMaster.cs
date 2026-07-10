using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.CleanRoomModule;
using Mooresmaster.Model.CleanRoomModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class CleanRoomMaster : IMasterValidator
    {
        // 閾値行はJSON順（清浄度が良い順）で保持する
        // Threshold rows are kept in JSON order (best air class first)
        public IReadOnlyList<ThresholdsElement> Thresholds { get; }
        public Pollution Pollution { get; }
        public bool IsAvailable { get; }

        // 全閾値超過（クリーンルーム外相当）を表す番兵インデックス
        // Sentinel index meaning all thresholds are exceeded (out of clean-room class)
        public int OutThresholdIndex => Thresholds.Count;

        private readonly CleanRoomMasterElement _element;
        private Dictionary<string, int> _thresholdIndexByClassName;
        private Dictionary<Guid, ChipDrawsElement> _chipDrawByMachineRecipeGuid;

        public CleanRoomMaster(JToken jToken)
        {
            var data = CleanRoomLoader.Load(jToken).Data;
            _element = data.Length == 0 ? null : data[0];
            Thresholds = _element?.Thresholds ?? Array.Empty<ThresholdsElement>();
            Pollution = _element?.Pollution;
            IsAvailable = Thresholds.Count > 0;
        }

        private CleanRoomMaster()
        {
            _element = null;
            Thresholds = Array.Empty<ThresholdsElement>();
            Pollution = null;
            IsAvailable = false;
        }

        // cleanRoom.json を持たないModのための空マスタ
        // Empty master for mods that ship no cleanRoom.json
        public static CleanRoomMaster CreateEmpty()
        {
            return new CleanRoomMaster();
        }

        public bool Validate(out string errorLogs)
        {
            return CleanRoomMasterUtil.Validate(_element, out errorLogs);
        }

        public void Initialize()
        {
            CleanRoomMasterUtil.Initialize(_element, out _thresholdIndexByClassName, out _chipDrawByMachineRecipeGuid);
        }

        // クラス名から閾値行インデックスを解決する（セーブ復元用、行順に依存しない）
        // Resolve a threshold row index by class name (for save restore, row-order independent)
        public bool TryGetThresholdIndexByClassName(string className, out int index)
        {
            return _thresholdIndexByClassName.TryGetValue(className, out index);
        }

        public bool TryGetChipDraw(Guid machineRecipeGuid, out ChipDrawsElement chipDraw)
        {
            return _chipDrawByMachineRecipeGuid.TryGetValue(machineRecipeGuid, out chipDraw);
        }
    }
}
