using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ResearchModule;
using Mooresmaster.Model.ResearchModule;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Core.Master
{
    public class ResearchMaster
    {
        public readonly Research Research;

        private readonly Dictionary<Guid, ResearchNodeMasterElement> _nodeMap = new();

        internal ResearchMaster(JToken researchJToken)
        {
            Research = ResearchLoader.Load(researchJToken);
            foreach (var node in Research.Data)
            {
                _nodeMap[node.ResearchNodeGuid] = node;
            }
        }

        public ResearchMaster(string researchJson)
            : this((JToken)JsonConvert.DeserializeObject(researchJson))
        {
        }

        public ResearchNodeMasterElement GetResearchNode(Guid id)
        {
            return _nodeMap[id];
        }

        public List<ResearchNodeMasterElement> GetAllNodes()
        {
            return Research.Data.ToList();
        }
    }
}
