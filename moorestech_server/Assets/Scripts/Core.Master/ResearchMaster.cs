using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ResearchModule;
using Mooresmaster.Model.ResearchModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ResearchMaster
    {
        public readonly Research Research;
        public readonly Dictionary<Guid, ResearchNodeMasterElement> ResearchElements;

        public ResearchMaster(JToken jToken)
        {
            Research = ResearchLoader.Load(jToken);
            ResearchElements = new Dictionary<Guid, ResearchNodeMasterElement>();
            foreach (var element in Research.Data)
            {
                ResearchElements[element.ResearchNodeGuid] = element;
            }
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