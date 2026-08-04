using System;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Core
{
    public class CharacterMasterTest
    {
        [Test]
        public void 空CharacterGuidはバリデーションで失敗する()
        {
            var master = CreateMaster(Guid.Empty);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("CharacterGuid", logs);
        }

        [Test]
        public void 重複CharacterGuidはバリデーションで失敗する()
        {
            var duplicatedGuid = Guid.Parse("10000000-0000-4000-8000-000000000001");
            var master = CreateMaster(duplicatedGuid, duplicatedGuid);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("CharacterGuid", logs);
        }

        private static CharacterMaster CreateMaster(params Guid[] characterGuids)
        {
            var characters = new JArray();
            for (var index = 0; index < characterGuids.Length; index++)
            {
                // 必須項目入り実マスタを構築
                // Build real master input with every required field
                characters.Add(new JObject
                {
                    ["characterGuid"] = characterGuids[index].ToString("D"),
                    ["characterId"] = $"character-{index}",
                    ["displayName"] = $"Character {index}",
                    ["modelAddresablePath"] = $"Character/Model/{index}",
                    ["skitModelAddresablePath"] = $"Character/SkitModel/{index}",
                });
            }

            return new CharacterMaster(new JObject
            {
                ["data"] = characters,
            });
        }
    }
}
