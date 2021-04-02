using System.Threading.Tasks;

namespace industrialization.GameSystem
{
    public class GameUpdate
    {
        public delegate void Update();

        public static event Update UpdateEvent;

        public static void StartUpdate()
        {
            Task.Run(ExeUpdate);
        }

        static void ExeUpdate()
        {
            while (true)
            {
                UpdateEvent();
            }
        }
    }
}