using System.Collections.Generic;
using CommandForgeGenerator.Command;

namespace Client.Game.Skit
{
    /// <summary>
    ///     エンティティ窓口に登録された全対象(エンティティ・ベルト上のアイテム)を一括表示切替
    ///     Toggles every target registered to the entity window (entities, items on belts) as a single unit
    /// </summary>
    internal class SkitEntityObjectControlGroup : ISkitEntityObjectControl
    {
        private readonly IReadOnlyList<ISkitEntityObjectControl> _entityObjectControls;

        internal SkitEntityObjectControlGroup(IReadOnlyList<ISkitEntityObjectControl> entityObjectControls)
        {
            _entityObjectControls = entityObjectControls;
        }

        public void SetActive(bool enable)
        {
            foreach (var entityObjectControl in _entityObjectControls) entityObjectControl.SetActive(enable);
        }
    }
}
