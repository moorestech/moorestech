using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
using Core.Master.Validator.MachineUpgrade;
using Mooresmaster.Loader.ResearchModule;
using Mooresmaster.Model.ResearchModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ResearchMaster : IMasterValidator
    {
        public readonly Research Research;
        public Dictionary<Guid, ResearchNodeMasterElement> ResearchElements { get; private set; }

        public ResearchMaster(JToken jToken)
        {
            Research = ResearchLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            ResearchMasterUtil.Validate(Research, out errorLogs);

            // 機械の強化系アクションは別ファイルで検証する（ResearchMasterUtil は200行超のため触らない）
            // Machine-upgrade actions are validated in a separate file (ResearchMasterUtil is over 200 lines, so it stays untouched)
            foreach (var node in Research.Data)
            {
                errorLogs += MachineUpgradeGameActionValidator.Validate(node.ClearedActions.items, $"Research:{node.ResearchNodeName}");
            }
            return string.IsNullOrEmpty(errorLogs);
        }

        public void Initialize()
        {
            ResearchMasterUtil.Initialize(Research, out var researchElements);
            ResearchElements = researchElements;
        }

        public ResearchNodeMasterElement GetResearch(Guid researchGuid)
        {
            return ResearchElements.GetValueOrDefault(researchGuid);
        }

        public List<ResearchNodeMasterElement> GetAllResearches()
        {
            return ResearchElements.Values.ToList();
        }
    }
}