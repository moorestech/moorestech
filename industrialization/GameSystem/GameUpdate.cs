using System.Collections.Generic;

namespace industrialization.GameSystem
{
    public class GameUpdate
    {
        private static List<IUpdate> _updates = new List<IUpdate>();
        public static void AddUpdateObject(IUpdate iUpdate)
        {
            _updates.Add(iUpdate);
        }

        public static void Update()
        {
            for (int i = _updates.Count - 1; 0 <= i ; i--)
            {
                _updates[i]?.Update();
            }
        }
    }
}