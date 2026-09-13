# ISaveMigrationStep のテンプレート

実装済みの実例が `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV1ToV2.cs`
にある（版1→版2: `world[].state` のJSON文字列をオブジェクトへ展開し、`currentTick`・`randomState`・
`miningCooldowns` を補う）。**新しいステップを書くときはまずそのファイルを読み、下の骨格へ写す。**

## 1. ステップ本体

`Game.SaveLoad/Migration/Steps/SaveMigrationStepV<n>ToV<n+1>.cs`:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    /// <summary>版2のセーブを版3へ上げる。<何をするか1行></summary>
    /// <summary>Raises a version 2 save to version 3: <one line in English></summary>
    public sealed class SaveMigrationStepV2ToV3 : ISaveMigrationStep
    {
        public int FromVersion => 2;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!TryConvertSomething(out var converted, out var failureReason)) return SaveMigrationStepResult.Failed(failureReason);

            // 何を何件変えたかを必ず1行残す。無音の縮退は禁止
            // Always leave one line saying what changed and how much; silent degradation is banned
            Debug.Log($"セーブを版2から版3へ変換しました。converted={converted}件");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            bool TryConvertSomething(out int converted, out string reason)
            {
                converted = 0;
                reason = null;

                if (!(save["world"] is JArray world))
                {
                    // 変換できない形は理由を持って返す。連鎖が版を刻まずBlockedにする
                    // Return the reason for a shape that cannot be converted; the chain then blocks without stamping the version
                    reason = $"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}";
                    return false;
                }

                foreach (var blockToken in world)
                {
                    if (!(blockToken is JObject block))
                    {
                        reason = $"world要素がオブジェクトではないため変換できません。 type={blockToken.Type}";
                        return false;
                    }

                    // 既に新形式のときは触らない（冪等）。塗り潰すと既存の値が無音で消える
                    // Leave values already in the new shape untouched (idempotent); overwriting erases them silently
                    if (block["newField"] != null) continue;

                    block["newField"] = 0;
                    converted++;
                }

                return true;
            }

            // 外部境界: セーブの値は任意の外部入力で、JSONでない綴り（base64-MessagePack等）を含みうる
            // External boundary: a save value is arbitrary external input and may not be JSON at all
            bool TryParseJson(string text, out JToken parsed)
            {
                try
                {
                    parsed = JToken.Parse(text);
                    return true;
                }
                catch (JsonReaderException)
                {
                    parsed = null;
                    return false;
                }
            }

            #endregion
        }
    }
}
```

変換できない値を素通ししてよいとき（JSONでない状態値など）は `Debug.Log` で理由を残す。
**変換したつもりで壊れた形が残る**ケース（V1→V2 の二重エンコード検知・非オブジェクトstateが実例）は
`SaveMigrationStepResult.Failed(reason)` を返す。例外は投げない — 連鎖が `Blocked` へ変換し、
版を刻まないまま `WorldLoaderFromJson` の理由付き中断としてプレイヤーへ届く（原本も無傷のまま残る）。

## 2. 登録

`moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` の
`SaveMigrationChain` 登録行（セーブ系登録ブロック内）へ足す:

```csharp
services.AddSingleton(new SaveMigrationChain(new ISaveMigrationStep[]
{
    new SaveMigrationStepV1ToV2(),
    new SaveMigrationStepV2ToV3(),
}, WorldSaveAllInfoV1.CurrentVersion));
```

同じPRで `WorldSaveAllInfoV1.CurrentVersion`（`Game.SaveLoad/Json/WorldVersions/`）を1つ上げる。
上げ忘れる（またはステップを足し忘れる）と、`SaveMigrationChain` の構築検証が
`マイグレーションステップのFromVersionが不正です。期待={1} 実際={1,2}（目標版=2）` の
`ArgumentException` を投げ、**起動時に**落ちる（意図した早期失敗）。

## 3. テスト

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV<n>ToV<n+1>Test.cs`。
既存の `SaveMigrationStepV1ToV2Test.cs` が雛形で、次の観点を1本ずつ持つ:

```csharp
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV2ToV3Test
    {
        [Test]
        public void 旧形式が新形式へ変換されるTest()
        {
            var save = JObject.Parse("{\"world\":[{\"blockGuid\":\"x\"}]}");

            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(0, result.Save["world"][0]["newField"].Value<int>());
        }

        [Test]
        public void 既に値がある項目は上書きされないTest() { /* 冪等 */ }

        [Test]
        public void 変換できない形はFailedで返るTest() { /* IsConverted=false と FailureReason を見る */ }

        [Test]
        public void FromVersionは2であるTest()
        {
            Assert.AreEqual(2, new SaveMigrationStepV2ToV3().FromVersion);
        }
    }
}
```

`Debug.LogError` を出す経路のテストは `LogAssert.Expect(LogType.Error, new Regex(...))` で受ける
（受けないとテストが失敗する）。連鎖側の昇順適用・欠番検知は `SaveMigrationChainTest` が既に持っているので、
ステップのテストでは連鎖を組み直さない。

## 4. 落とし穴

- **`worldVersion` はステップで触らない。** `SaveMigrationChain` が1手ごとに `FromVersion + 1` を書く。
- **マスタを引かない。** マスタに無い guid の始末は `MissingMasterPruner`（連鎖の後段）の仕事。
  ステップの中で `MasterHolder` を引くと、削除済みマスタで変換自体が落ちる。
- **前の版のセーブクラスを参照しない。** 変換は `JObject` の上だけで行う。`WorldSaveAllInfoV1` を
  旧版形状で読もうとすると、次の形式変更のたびに過去のステップが壊れる。
- **冪等にする。** 既にキーが在るときは足さない・上書きしない。バックアップから戻して再実行する運用がある。
- **`Migration/` 直下は10ファイル上限に近い。** ステップは必ず `Migration/Steps/` へ置く。
- **新規サーバー側 `.cs` を足した直後は Unity 再起動が要る**（`uloop launch ./moorestech_client --restart`）。
  file: パッケージ参照のため Refresh では検出されない。
