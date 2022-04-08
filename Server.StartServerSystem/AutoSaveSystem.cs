using System;
using System.Threading.Tasks;
using Game.Save.Interface;

namespace Server.StartServerSystem
{
    public class AutoSaveSystem
    {
        private readonly ISaveRepository _saveRepository;

        public AutoSaveSystem(ISaveRepository saveRepository)
        {
            _saveRepository = saveRepository;
        }

        public async Task AutoSave()
        {
            while (true)
            {
                Console.WriteLine("オートセーブ実行");
                await Task.Delay(TimeSpan.FromSeconds(10));
                _saveRepository.Save();
            }
        }
    }
}