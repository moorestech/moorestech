using System;
using System.Collections.Generic;
using System.Linq;
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
        private Dictionary<Guid, ModuleMasterElement> _moduleGuidTable;

        public ModuleMaster(JToken jToken)
        {
            Modules = ModulesLoader.Load(jToken);
        }

        public ModuleMasterElement GetModuleElement(Guid moduleGuid)
        {
            return _moduleGuidTable[moduleGuid];
        }

        public ModuleMasterElement GetModuleElementByItemGuidOrNull(Guid itemGuid)
        {
            return Modules.Data.FirstOrDefault(x => x.ItemGuid == itemGuid);
        }

        public bool Validate(out string errorLogs)
        {
            // itemGuid が ItemMaster に存在することを検証
            // Validate that each module's itemGuid exists in ItemMaster
            errorLogs = "";
            foreach (var module in Modules.Data)
            {
                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(module.ItemGuid);
                if (itemId == null) errorLogs += $"[ModuleMaster] Name:{module.Name} has invalid ItemGuid:{module.ItemGuid}\n";
            }
            return errorLogs.Length == 0;
        }

        public void Initialize()
        {
            _moduleGuidTable = Modules.Data.ToDictionary(x => x.ModuleGuid, x => x);
        }
    }
}
