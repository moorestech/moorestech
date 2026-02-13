---
name: creating-server-tests
description: >
  moorestech_serverのNUnitテストを作成するスキル。テストの雛形生成、初期化パターン、命名規約、
  テスト用IDの使い方を含む。
  Use when:
  (1) moorestech_serverに新しいテストクラスを追加する時
  (2) 「テストを書いて」「テストを作成して」とサーバー側のテスト作成を依頼された時
  (3) 既存機能のテストカバレッジを追加する時
  (4) CombinedTest/UnitTest/PacketTestのいずれかを作成する時
---

# Server Test Creator

moorestech_serverのテスト作成ガイド。NUnit + Unity Test Frameworkを使用。

## テスト種別の選択

| 種別 | 用途 | 配置先 |
|------|------|--------|
| UnitTest | 単一クラス・メソッドの検証 | `Tests/UnitTest/{Layer}/{Category}/` |
| CombinedTest | 複数システムの統合検証 | `Tests/CombinedTest/{Layer}/` |
| PacketTest | プロトコル通信の検証 | `Tests/CombinedTest/Server/PacketTest/` |

テストファイルは `moorestech_server/Assets/Scripts/Tests/` 配下の `Server.Tests.asmdef` に含まれる。

## ワークフロー

### 1. テスト種別に応じたテンプレートを選択

**UnitTest** - 単一機能テスト:
```csharp
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Game.Context;

namespace Tests.UnitTest.{Layer}.{Category}
{
    public class {Feature}Test
    {
        [SetUp]
        public void Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void {動作内容}Test()
        {
            // テストコード
            Assert.AreEqual(expected, actual);
        }
    }
}
```

**CombinedTest** - 統合テスト:
```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.{Layer}
{
    public class {Feature}Test
    {
        [Test]
        public void {動作内容}Test()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ブロック配置
            // Place blocks
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.ChestId,
                new Vector3Int(0, 0, 0),
                BlockDirection.North,
                Array.Empty<BlockCreateParam>(),
                out var block);

            var component = block.GetComponent<TargetComponent>();

            // ゲーム更新
            // Update game
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(expected, actual);

            #region Internal

            void HelperMethod() { }

            #endregion
        }
    }
}
```

**PacketTest** - テンプレートは [references/packet-test.md](references/packet-test.md) を参照。

### 2. 必須ルール

- **テスト用IDは `ForUnitTestModBlockId` / `ForUnitTestItemId` から取得** - マジックナンバー禁止
- **コメントは日英2行セット** で記述
- **try-catch禁止** - 条件分岐で対応
- **デフォルト引数禁止** - 呼び出し側を変更
- **複雑なテストでは `#region Internal` + ローカル関数** を使用
- **`#endregion` の下にコードを書かない**
- 新しいブロックIDが必要な場合は `ForUnitTestModBlockId.cs` と `blocks.json` を更新

### 3. テスト実行

作成後、MCPツールまたはシェルスクリプトでテスト実行:

```
# MCPツール（推奨）
mcp__moorestech_server__RunEditModeTests
  groupNames: ["^Tests\\.{TestType}\\.{Layer}\\.{ClassName}$"]

# シェルスクリプト（フォールバック）
./tools/unity-test.sh moorestech_server "^Tests\\.{TestType}\\.{Layer}\\.{ClassName}$"
```

## リソース

- [references/initialization.md](references/initialization.md) - ディレクトリ構造、DI初期化、ServerContext/ServiceProviderアクセス
- [references/unit-test.md](references/unit-test.md) - UnitTestテンプレート、パラメータ化テスト
- [references/combined-test.md](references/combined-test.md) - CombinedTestテンプレート、ブロック配置API、ゲーム更新パターン
- [references/packet-test.md](references/packet-test.md) - パケットテストテンプレート
- [references/test-ids-and-helpers.md](references/test-ids-and-helpers.md) - テスト用ID一覧、ヘルパークラス（DummyBlockInventory等）
- [references/coding-conventions.md](references/coding-conventions.md) - コメント規約、namespace規約、禁止事項
