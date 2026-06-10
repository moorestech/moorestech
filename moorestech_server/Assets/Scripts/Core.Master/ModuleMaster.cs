using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.ModulesModule;
using Mooresmaster.Model.ModulesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // モジュール定義(modules.json)を保持し、GUIDで引けるようにするマスタ
    // Master that holds module definitions and resolves them by GUID
    public class ModuleMaster : IMasterValidator
    {
        public readonly Modules Modules;
        private Dictionary<Guid, ModuleMasterElement> _itemGuidTable;

        public ModuleMaster(JToken jToken)
        {
            Modules = ModulesLoader.Load(jToken);
        }

        public ModuleMasterElement GetModuleElementByItemGuidOrNull(Guid itemGuid)
        {
            return _itemGuidTable.GetValueOrDefault(itemGuid);
        }

        public bool Validate(out string errorLogs)
        {
            return ModuleMasterUtil.Validate(Modules, out errorLogs);
        }

        public void Initialize()
        {
            ModuleMasterUtil.Initialize(Modules, out _itemGuidTable);
        }
    }
}
