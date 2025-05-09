namespace Client.Game.MovieTutorial.GameObjectMovie
{
    public class GameObjectMovieTutorialParameter : IMovieTutorialParameter
    {
        public readonly GamObjectMovieSequence Sequence;
        
        
        public GameObjectMovieTutorialParameter(GamObjectMovieSequence sequence)
        {
            Sequence = sequence;
        }
    }
}