using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
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
            return ResearchMasterUtil.Validate(Research, out errorLogs);
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