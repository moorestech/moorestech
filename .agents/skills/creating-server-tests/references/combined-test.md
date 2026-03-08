# CombinedTestパターン

## 基本構造

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class {Feature}Test
    {
        [Test]
        public void {テスト内容}Test()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ブロック配置
            // Place block
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.ChestId,
                new Vector3Int(0, 0, 0),
                BlockDirection.North,
                Array.Empty<BlockCreateParam>(),
                out var block);

            // コンポーネント取得
            // Get component
            var component = block.GetComponent<VanillaChestComponent>();

            // ゲーム更新
            // Update game tick
            GameUpdater.UpdateOneTick();

            // アサーション
            Assert.AreEqual(expected, actual);

            #region Internal

            // ヘルパーメソッド（ローカル関数）
            void HelperMethod() { }

            #endregion
        }
    }
}
```

## ブロック配置API

```csharp
// WorldBlockDatastore経由（座標指定）
ServerContext.WorldBlockDatastore.TryAddBlock(
    ForUnitTestModBlockId.ChestId,     // ブロックID
    Vector3Int.one,                     // 配置座標
    BlockDirection.North,               // 方向
    Array.Empty<BlockCreateParam>(),    // 生成パラメータ
    out var block);                     // 出力

// コンポーネント取得
var component = block.GetComponent<VanillaChestComponent>();
```

## ゲーム更新パターン

```csharp
// 1ティック更新
GameUpdater.UpdateOneTick();

// 条件が満たされるまでループ
while (!condition) GameUpdater.UpdateOneTick();

// 時間ベース更新
var ticks = (int)(seconds * GameUpdater.TicksPerSecond);
for (var i = 0; i < ticks; i++)
{
    GameUpdater.RunFrames(1);
}
```
