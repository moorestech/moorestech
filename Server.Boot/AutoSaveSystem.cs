using System;
using System.Threading.Tasks;
using Game.Save.Interface;

namespace Server.Boot
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
                await Task.Delay(TimeSpan.FromSeconds(30));
                _saveRepository.Save();
            }
        }
    }
}