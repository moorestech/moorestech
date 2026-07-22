using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class ConnectToolUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, ConnectToolUnlockStateInfo> _infos = new();

        public ConnectToolUnlockStateHolder()
        {
            foreach (var element in MasterHolder.ConnectToolMaster.All)
            {
                if (_infos.ContainsKey(element.ConnectToolGuid)) continue;
                _infos.Add(element.ConnectToolGuid, new ConnectToolUnlockStateInfo(element.ConnectToolGuid, element.InitialUnlocked));
            }
        }

        public void Unlock(Guid connectToolGuid)
        {
            if (!_infos.ContainsKey(connectToolGuid))
            {
                Debug.LogError($"[UnlockConnectTool] Connect tool not found: {connectToolGuid}");
                return;
            }
            _infos[connectToolGuid].Unlock();
            _onUnlock.OnNext(connectToolGuid);
        }

        public void Load(List<ConnectToolUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                // マスタに存在しない接続ツールはスキップ
                // Skip connect tools that don't exist in master
                if (MasterHolder.ConnectToolMaster.GetElementOrNull(Guid.Parse(jsonObject.ConnectToolGuid)) == null) continue;
                var state = new ConnectToolUnlockStateInfo(jsonObject);
                _infos[state.ConnectToolGuid] = state;
            }
        }

        public List<ConnectToolUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new ConnectToolUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
