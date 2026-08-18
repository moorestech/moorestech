using System.Collections.Generic;
using CommandForgeGenerator.Command;

namespace Client.Game.Skit
{
    /// <summary>
    ///     Environment外世界オブジェクトを一括表示切替
    ///     Toggles every world object placed outside Environment as a single unit
    /// </summary>
    internal class SkitWorldObjectControlGroup : ISkitWorldObjectControl
    {
        private readonly IReadOnlyList<ISkitWorldObjectControl> _worldObjectControls;

        internal SkitWorldObjectControlGroup(IReadOnlyList<ISkitWorldObjectControl> worldObjectControls)
        {
            _worldObjectControls = worldObjectControls;
        }

        public void SetActive(bool enable)
        {
            foreach (var worldObjectControl in _worldObjectControls) worldObjectControl.SetActive(enable);
        }
    }
}
