using Cysharp.Threading.Tasks;

namespace Client.Starter
{
    public interface IInitializeSequence
    {
        public UniTask Process();
    }
}