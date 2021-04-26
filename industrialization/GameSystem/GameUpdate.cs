namespace industrialization.GameSystem
{
    public class GameUpdate
    {
        public delegate void Update();
        public static event Update UpdateEvent;
        
        
    }
}