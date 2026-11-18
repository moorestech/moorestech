using System.Collections.Generic;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究ノード定義の列。生成時に1回組む
    /// Research node definitions, built once
    /// </summary>
    public sealed class ResearchNodeCatalog
    {
        public IReadOnlyList<ResearchNodeDefinition> Nodes { get; }

        public ResearchNodeCatalog()
        {
            Nodes = ResearchNodeDefinitionsFromMaster.Build();
        }
    }
}
