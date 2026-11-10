using System.Collections.Generic;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究ノードの定義の列。マスタはゲーム中に変わらないので生成時に 1 回だけ組む
    /// The list of research node definitions; the master never changes in play, so it is built once at construction
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
