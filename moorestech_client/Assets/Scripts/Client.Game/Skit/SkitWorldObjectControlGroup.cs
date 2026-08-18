using System.Collections.Generic;
using CommandForgeGenerator.Command;

namespace Client.Game.Skit
{
    /// <summary>
    ///     Environment外に置かれた世界オブジェクトを1単位で表示切替する
    ///     Toggles every world object placed outside Environment as a single unit
    /// </summary>
    public class SkitWorldObjectControlGroup : ISkitWorldObjectControl
    {
        private readonly IReadOnlyList<ISkitWorldObjectControl> _worldObjectControls;

        public SkitWorldObjectControlGroup(IReadOnlyList<ISkitWorldObjectControl> worldObjectControls)
        {
            _worldObjectControls = worldObjectControls;
        }

        public void SetActive(bool enable)
        {
            foreach (var worldObjectControl in _worldObjectControls) worldObjectControl.SetActive(enable);
        }
    }
}
