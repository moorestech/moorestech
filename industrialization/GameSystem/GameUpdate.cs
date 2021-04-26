using System.Collections.Generic;

namespace industrialization.GameSystem
{
    public class GameUpdate
    {
        private static List<IUpdate> _updates = new List<IUpdate>();
        public static void AddUpdate(IUpdate iUpdate)
        {
            _updates.Add(iUpdate);
        }

        public static void Update()
        {
            foreach (var update in _updates)
            {
                if (update == null)
                {
                    _updates.Remove(update);
                }
                else
                {
                    update.Update();
                }
            }
        }
    }
}