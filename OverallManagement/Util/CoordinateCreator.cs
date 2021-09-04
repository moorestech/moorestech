using industrialization.OverallManagement.DataStore;

namespace industrialization.OverallManagement.Util
{
    public class CoordinateCreator
    {
        public static Coordinate New(int X,int Y)
        {
            return new Coordinate()
            {
                x = X,
                y = Y
            };
        }
    }
}