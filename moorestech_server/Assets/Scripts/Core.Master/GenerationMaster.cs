using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.GenerationModule;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // マップ生成設定のマスタ。ロードしたmod自身のgeneration.jsonを保持し、
    // priority最大の1件をSelectedGenerationとして選出する（未定義ならnull）。
    // Master for map-generation config: holds the loaded mod's own generation.json
    // and selects the max-priority entry as SelectedGeneration (null when undefined).
    public class GenerationMaster : IMasterValidator
    {
        public readonly Generation Generation;

        // NOTE: MasterHolder.GetJsonは現状index=0固定で単一modのみロードするため、
        // 選択候補は常に1件。複数mod対応はTask 6で見直す。
        // NOTE: MasterHolder.GetJson currently hard-codes index=0 (single-mod load only),
        // so there is always exactly one candidate here; Task 6 revisits multi-mod loading.
        private readonly string _modId;

        public Generation SelectedGeneration { get; private set; }
        public bool HasSelectedGeneration => SelectedGeneration != null;

        public GenerationMaster(JToken jToken, string modId)
        {
            Generation = GenerationLoader.Load(jToken);
            _modId = modId;
        }

        private GenerationMaster()
        {
            Generation = null;
            _modId = null;
        }

        // generation.jsonを持たないModのための空マスタ（algorithm:None相当・選択対象外）
        // Empty master for mods that ship no generation.json (equivalent to algorithm: None, excluded from selection)
        public static GenerationMaster CreateEmpty()
        {
            return new GenerationMaster();
        }

        public bool Validate(out string errorLogs)
        {
            if (Generation == null)
            {
                errorLogs = "";
                return true;
            }

            return GenerationMasterUtil.Validate(Generation, out errorLogs);
        }

        public void Initialize()
        {
            if (Generation == null)
            {
                SelectedGeneration = null;
                return;
            }

            var candidates = new List<(Generation Element, string ModId)> { (Generation, _modId) };
            SelectedGeneration = GenerationSelection.Select(candidates);
        }
    }
}
