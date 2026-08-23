# 最寄りmapObject/露頭探索のk-d tree索引化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** チュートリアルピンが毎フレーム行う「最寄りのmapObject／露頭」探索を、全走査（2002件のGuidパース＋663件の距離計算）からk-d tree索引（30〜80ノード訪問）に置き換え、木チュートリアル中のフレーム負荷を根治する。

**Architecture:** 座標だけを知る汎用索引 `NearestTargetIndex<T>`（guid別の `KdTree<T>`）を `Client.Game/InGame/Map/NearestSearch/` に新設し、`MapObjectGameObjectDatastore`（破壊あり→dirtyフラグで次の探索時に生存個体だけで再構築）と `OutcropGameObjectDatastore`（破壊なし→生成完了時に1回構築）の両方を載せ替える。「破壊済み」「可否」の判断は具体側（Datastore側）で行い、索引にはフィルタ済みの点集合をプッシュする。k-d treeは点集合が静的（起動時生成後は座標不変・追加なし・破壊のみ）という前提で、構築時に座標を焼き込みフラット配列の暗黙平衡木として持つ。

**Tech Stack:** Unity C# (`Client.Game` asmdef) / NUnit EditMode (`Client.Tests`) / UniRx（本planでは新規イベント無し）

## Requirements

裁定原本: `.decisions/2026-08-23-最寄りmapObject探索はk-d treeで索引化する.md`（bd: moorestech-8tw6）

- R1. 最寄り探索はk-d treeによる空間索引で行う。挙動（毎フレーム厳密な最寄りを取り直す）は変えない — 受け入れ: 線形全走査と同一の結果（同距離タイは距離が一致）をランダム点・タイ・1件・空・境界で検証するテストが通る
- R2. 汎用索引を1つ作り、mapObject（2002件/7guid）と露頭（1775件/11guid）の両方を載せる。`OutcropGuidIndex` は廃止 — 受け入れ: `OutcropGuidIndex.cs` が存在せず、両Datastoreが `NearestTargetIndex<T>` を使う
- R3. 索引は「木」「露頭」「破壊済み」「可否」というドメイン語彙を知らず座標のみを知る（`INearestSearchTarget` は `Vector3 Position { get; }` のみ） — 受け入れ: `NearestSearch/` 配下に `Client.Game` 以外への参照・`IsDestroyed`/`IsAvailable` 等の語が出ない
- R4. 破壊の反映は「破壊イベントで該当guidにdirtyを立てる→次にそのguidを探索した時に生存個体（`IsAvailable`）だけで再構築」 — 受け入れ: 破壊済み個体がdirty再構築後に返らないテストが通る
- R5. guid別に分離され、別guidの個体を返さない — 受け入れ: 2guidを登録し近い別guid個体が返らないテストが通る
- R6. 索引は座標を構築時に焼き込み、探索時に `transform.position` を読まない — 受け入れ: `KdTree<T>` の探索パスに `Position` 読み出しが無い
- R7. `MapObjectGameObject.MapObjectGuid` はパース済み `Guid` を返す（呼ぶたびの string→Guid パースをやめる）
- R8. `MapObjectPin`/`VeinPin` の死コード（`transform.LookAt`＋`Quaternion.Euler`）を削除する。`MapObjectPin` の対象不在 `Debug.LogError` は対象guidごと1回にする（`VeinPin` と同形）
- R9. `WorldPinStateStore.SetPin` の `FirstOrDefault` と `CreateData` の `Select().ToArray()` を非LINQのループに置き換える（毎フレームの経路からアロケーションを除く）
- やらないこと: Webオーバーレイへの毎フレーム配信（JSON＋WebSocket）の間引き・配信レート変更（別bd moorestech-rw09）。k-d treeへの動的挿入・削除API。2D距離化。

## Global Constraints

- AGENTS.md 全規約（1ファイル200行以下・1ディレクトリ10ファイル以下・partial禁止・`Func<>`禁止・default引数禁止・try-catch禁止・イベントはUniRx・日英2行コメント・`#region Internal`はローカル関数限定・初期化メソッド名は`Initialize`・`[SerializeField]`は小文字キャメル）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client` を通す。テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`
- 作業はタスク用worktree（`moores-wt new feature/nearest-search-kd-tree`）で行う。メインワークツリーでのUnity起動・ブランチ操作は禁止（CLAUDE.local.md）
- 距離は3D（`sqrMagnitude` 比較）。線形全走査の現行実装と一致させるため平方距離で比較し `sqrt` は取らない
- 探索パス（`SearchNearest`）はアロケーションゼロ（毎フレーム呼ばれる）。構築（`SetTargets`）はイベント時のみなのでアロケーション可
- k-d treeのタイブレーク規約: 「厳密に近い候補だけが最良値を更新する（`<`）。等距離は走査順で先に訪れた側が残る」。テストは等距離ケースでは距離一致のみを検証し、個体同一性は最寄りが一意なケースでのみ検証する

---

## レイヤリング制約（配置と前例）

| 項目 | 配置 | 前例・根拠 |
|---|---|---|
| `INearestSearchTarget` / `KdTree<T>` / `NearestTargetIndex<T>` | `Client.Game/InGame/Map/NearestSearch/`（`Client.Game` asmdef） | 利用側が `Client.Game/InGame/Map/MapObject` と `Map/Outcrop` の2つで両方 `Client.Game` 内。`Map/` 直下の兄弟ディレクトリとして置く。`Core.*`/`Client.Common` へは出さない（利用側が1アセンブリに閉じているため昇格の根拠が無い） |
| `MapObjectNearestSearcher`（dirty管理＋生存フィルタ＋索引プッシュ） | `Client.Game/InGame/Map/MapObject/` | 現 `SearchNearestMapObject` の置き場（Datastore）と同じディレクトリ。Datastoreが198行で200行規約に当たるため探索責務を別クラスへ切り出す。`OutcropGuidIndex`（Datastore隣に置いた探索補助クラス）と同形 |
| 露頭側の索引保持 | `OutcropGameObjectDatastore` のフィールド（160行に収まる） | `OutcropGuidIndex` の役割をDatastore直下の `Dictionary`＋`NearestTargetIndex` に畳む。破壊経路が無いのでdirty管理クラスは不要 |
| dirtyの立て方 | `OnUpdateMapObject` の Destroy 分岐直後に `MarkDirty(guid)` を明示呼び出し | 「変化を起こす操作の直後にプッシュ」（AGENTS.md 状態変化の検知）。`MapObjectGameObject.OnDestroyMapObject` の購読は使わない（Datastore自身が `DestroyMapObject()` を呼ぶ当事者のため購読は遠回り） |
| テスト | `Client.Tests/Map/NearestSearch/`（純C#テスト）＋ `Client.Tests/Map/MapObjectNearestSearcherTest.cs`（`MapObjectGameObject` 実体を使うEditModeテスト） | `Client.Tests/Map/MapObjectHpBarScaleTest.cs`（サーバDI＋`SetRuntimeIdentity`＋`Initialize`で実体を組む前例） |

## データフロー地図

```
（Layout応答）→ Datastore生成ループ → [guid別List + dirty] → SearchNearest時にIsAvailableでフィルタ → NearestTargetIndex.SetTargets（焼き込み）
（破壊イベント）→ Datastore.OnUpdateMapObject → MarkDirty(guid)
（毎フレーム）MapObjectPin.Update → Datastore.SearchNearestMapObject → Searcher（dirtyなら再構築）→ KdTree.SearchNearest → ピン座標
```
新規コンポーネントは既存フローの「読み手」（索引は派生データ）。交差点は追加しない。

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| mapObjectピンが最寄りの未破壊個体を指す | 生きる | R1/R4 |
| 木を伐採した直後に次の最寄りへピンが移る | 生きる | 破壊イベント→dirty→次フレームの探索で再構築 |
| 対象が全滅したときのLogError | 生きる（1回化） | R8 |
| 露頭ピンが最寄り露頭を指す・不在時に隠れる | 生きる | 露頭側は生成完了時に構築・`SearchNearest` がguid不在でnull |
| Webオーバーレイへのピン配信 | 生きる | `WorldPinStateStore` の挙動は変えずアロケーションのみ除去（R9） |

---

### Task 1: `INearestSearchTarget` と `KdTree<T>`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/NearestSearch/INearestSearchTarget.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/NearestSearch/KdTree.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/NearestSearch/NearestSearchTestTarget.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/NearestSearch/KdTreeTest.cs`

**Interfaces:**
- Produces: `public interface INearestSearchTarget { Vector3 Position { get; } }`
- Produces: `public sealed class KdTree<T> where T : class, INearestSearchTarget` — `public KdTree(IReadOnlyList<T> targets)`（構築時に `Position` を焼き込む）／`public T SearchNearest(Vector3 query)`（空なら `null`）／`public int Count { get; }`

- [x] **Step 1: テスト用ターゲットと失敗するテストを書く**

`Client.Tests/Map/NearestSearch/NearestSearchTestTarget.cs`:
```csharp
using Client.Game.InGame.Map.NearestSearch;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     索引テスト用の座標だけを持つターゲット
    ///     Position-only target for index tests
    /// </summary>
    public sealed class NearestSearchTestTarget : INearestSearchTarget
    {
        public Vector3 Position { get; }

        public NearestSearchTestTarget(Vector3 position)
        {
            Position = position;
        }
    }
}
```

`Client.Tests/Map/NearestSearch/KdTreeTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     k-d treeの最寄り結果が線形全走査と一致することを検証
    ///     Verifies k-d tree nearest results match a linear full scan
    /// </summary>
    public class KdTreeTest
    {
        [Test]
        public void ランダム点群で線形走査と最寄りが一致する()
        {
            // 決定論のため固定シード。5km四方×高低差を模した分布
            // Fixed seed for determinism; spread mimics the 5km world with height variation
            var random = new System.Random(20260823);
            var targets = new List<NearestSearchTestTarget>();
            for (var i = 0; i < 2000; i++) targets.Add(new NearestSearchTestTarget(RandomPoint(random)));
            var tree = new KdTree<NearestSearchTestTarget>(targets);

            for (var i = 0; i < 500; i++)
            {
                var query = RandomPoint(random);
                var expected = LinearNearest(targets, query);
                var actual = tree.SearchNearest(query);
                Assert.AreSame(expected, actual, $"query={query}");
            }
        }

        [Test]
        public void 同距離タイでも距離は線形走査と一致する()
        {
            // 原点対称の8点。どれが返っても距離は同じ
            // Eight origin-symmetric points; whichever returns, the distance is equal
            var targets = new List<NearestSearchTestTarget>();
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                targets.Add(new NearestSearchTestTarget(new Vector3(x, y, z)));
            var tree = new KdTree<NearestSearchTestTarget>(targets);

            var query = Vector3.zero;
            var expectedDistance = (LinearNearest(targets, query).Position - query).sqrMagnitude;
            var actual = tree.SearchNearest(query);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expectedDistance, (actual.Position - query).sqrMagnitude, 1e-6f);
        }

        [Test]
        public void 一件だけの木はその一件を返す()
        {
            var only = new NearestSearchTestTarget(new Vector3(10f, 0f, -5f));
            var tree = new KdTree<NearestSearchTestTarget>(new[] { only });
            Assert.AreSame(only, tree.SearchNearest(new Vector3(-999f, 50f, 999f)));
        }

        [Test]
        public void 空の木はnullを返す()
        {
            var tree = new KdTree<NearestSearchTestTarget>(new List<NearestSearchTestTarget>());
            Assert.AreEqual(0, tree.Count);
            Assert.IsNull(tree.SearchNearest(Vector3.zero));
        }

        [Test]
        public void 分割面の反対側にある真の最寄りを取りこぼさない()
        {
            // x軸中央値で分割されるよう配置し、クエリは分割面のすぐ左・最寄りは面のすぐ右に置く
            // Arrange so the root splits on x; query sits just left of the plane, the true nearest just right of it
            var targets = new List<NearestSearchTestTarget>
            {
                new(new Vector3(-100f, 0f, 0f)),
                new(new Vector3(-50f, 0f, 0f)),
                new(new Vector3(0f, 0f, 0f)),
                new(new Vector3(0.5f, 0f, 0f)),
                new(new Vector3(100f, 0f, 0f)),
            };
            var tree = new KdTree<NearestSearchTestTarget>(targets);
            var query = new Vector3(0.3f, 0f, 0f);
            Assert.AreSame(LinearNearest(targets, query), tree.SearchNearest(query));
        }

        [Test]
        public void 同一座標が多数あっても構築と探索が成立する()
        {
            // 露頭は同一AABB中心に複数鉱脈が重なりうる。同座標の連続で再帰が破綻しないこと
            // Outcrops can stack on one AABB center; runs of identical coordinates must not break recursion
            var targets = new List<NearestSearchTestTarget>();
            for (var i = 0; i < 300; i++) targets.Add(new NearestSearchTestTarget(new Vector3(7f, 7f, 7f)));
            targets.Add(new NearestSearchTestTarget(new Vector3(8f, 7f, 7f)));
            var tree = new KdTree<NearestSearchTestTarget>(targets);
            Assert.AreSame(targets[300], tree.SearchNearest(new Vector3(9f, 7f, 7f)));
        }

        private static Vector3 RandomPoint(System.Random random)
        {
            return new Vector3(
                (float)(random.NextDouble() * 5000.0 - 2500.0),
                (float)(random.NextDouble() * 200.0),
                (float)(random.NextDouble() * 5000.0 - 2500.0));
        }

        private static NearestSearchTestTarget LinearNearest(List<NearestSearchTestTarget> targets, Vector3 query)
        {
            NearestSearchTestTarget nearest = null;
            var nearestSqr = float.MaxValue;
            foreach (var target in targets)
            {
                var sqr = (target.Position - query).sqrMagnitude;
                if (nearestSqr <= sqr) continue;
                nearest = target;
                nearestSqr = sqr;
            }

            return nearest;
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗（型未定義）を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `INearestSearchTarget` / `KdTree` が見つからないエラー

- [x] **Step 3: `INearestSearchTarget` と `KdTree<T>` を実装する**

`Client.Game/InGame/Map/NearestSearch/INearestSearchTarget.cs`:
```csharp
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     最寄り探索の対象。索引は座標だけを知り、可否や破壊状態は利用側が判断する
    ///     Target of nearest search; the index knows only the position, availability is decided by the caller
    /// </summary>
    public interface INearestSearchTarget
    {
        Vector3 Position { get; }
    }
}
```

`Client.Game/InGame/Map/NearestSearch/KdTree.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     静的な点集合向けの3次元k-d tree。構築時に座標を焼き込み、配列を中央値で再帰分割した暗黙平衡木として持つ
    ///     3D k-d tree for a static point set; positions are baked at build time into an implicit balanced tree over a median-split array
    /// </summary>
    public sealed class KdTree<T> where T : class, INearestSearchTarget
    {
        private const int AxisCount = 3;

        // 中央値分割済みの配列。区間[lo,hi)の中央がノード、左右の部分区間が子
        // Median-split arrays; the middle of [lo,hi) is the node and the two halves are its children
        private readonly Vector3[] _positions;
        private readonly T[] _targets;

        // 探索中の最良値。毎フレーム呼ばれるためアロケーションを避けてフィールドに持つ
        // Best-so-far during a search; kept in fields to avoid allocating on the per-frame path
        private Vector3 _query;
        private int _bestIndex;
        private float _bestSqrDistance;

        public int Count => _targets.Length;

        public KdTree(IReadOnlyList<T> targets)
        {
            _positions = new Vector3[targets.Count];
            _targets = new T[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                _targets[i] = targets[i];
                _positions[i] = targets[i].Position;
            }

            Build(0, _targets.Length, 0);
        }

        public T SearchNearest(Vector3 query)
        {
            if (_targets.Length == 0) return null;

            _query = query;
            _bestIndex = -1;
            _bestSqrDistance = float.MaxValue;
            Search(0, _targets.Length, 0);
            return _targets[_bestIndex];
        }

        private void Build(int lo, int hi, int depth)
        {
            // 1点以下の区間は葉
            // A range of one point or fewer is a leaf
            if (hi - lo <= 1) return;

            // 軸で整列して中央値をノードにし、左右を次の軸で再帰する（同一座標の連続でも区間は必ず縮む）
            // Sort by axis, take the median as the node, recurse both halves on the next axis (ranges shrink even for identical coordinates)
            var axis = depth % AxisCount;
            System.Array.Sort(_positions, _targets, lo, hi - lo, AxisComparer.ForAxis(axis));
            var mid = (lo + hi) / 2;
            Build(lo, mid, depth + 1);
            Build(mid + 1, hi, depth + 1);
        }

        private void Search(int lo, int hi, int depth)
        {
            if (hi <= lo) return;

            var mid = (lo + hi) / 2;
            var axis = depth % AxisCount;
            var nodePosition = _positions[mid];

            // 厳密に近い候補だけが最良値を更新する（等距離は走査順で先に訪れた側が残る）
            // Only a strictly closer candidate updates the best (ties keep whichever was visited first)
            var sqrDistance = (nodePosition - _query).sqrMagnitude;
            if (sqrDistance < _bestSqrDistance)
            {
                _bestSqrDistance = sqrDistance;
                _bestIndex = mid;
            }

            // クエリのある側を先に降り、分割面までの距離が最良値より近い時だけ反対側も見る
            // Descend the query's side first, then the far side only if the splitting plane is closer than the best
            var delta = _query[axis] - nodePosition[axis];
            if (delta < 0f)
            {
                Search(lo, mid, depth + 1);
                if (delta * delta < _bestSqrDistance) Search(mid + 1, hi, depth + 1);
            }
            else
            {
                Search(mid + 1, hi, depth + 1);
                if (delta * delta < _bestSqrDistance) Search(lo, mid, depth + 1);
            }
        }

        /// <summary>
        ///     軸ごとの座標比較。構築時のみ使うため軸数分を静的に共有する
        ///     Per-axis position comparer; build-time only, so one instance per axis is shared statically
        /// </summary>
        private sealed class AxisComparer : IComparer<Vector3>
        {
            private static readonly AxisComparer[] Comparers = { new(0), new(1), new(2) };
            private readonly int _axis;

            private AxisComparer(int axis)
            {
                _axis = axis;
            }

            public static AxisComparer ForAxis(int axis)
            {
                return Comparers[axis];
            }

            public int Compare(Vector3 left, Vector3 right)
            {
                return left[_axis].CompareTo(right[_axis]);
            }
        }
    }
}
```

- [x] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "KdTreeTest"`
Expected: 6件 PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/NearestSearch moorestech_client/Assets/Scripts/Client.Tests/Map/NearestSearch
git commit -m "feat(nearest-search): 静的点集合向けの3次元k-d treeを追加"
```
（`.meta` はUnityが生成したものをそのままコミットする。手動作成しない）

---

### Task 2: `NearestTargetIndex<T>`（guid別のk-d tree集合）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/NearestSearch/NearestTargetIndex.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/NearestSearch/NearestTargetIndexTest.cs`

**Interfaces:**
- Consumes: Task 1 の `KdTree<T>`, `INearestSearchTarget`
- Produces: `public sealed class NearestTargetIndex<T> where T : class, INearestSearchTarget` — `public void SetTargets(Guid key, IReadOnlyList<T> targets)`（同keyは上書き＝再構築）／`public T SearchNearest(Guid key, Vector3 position)`（key未登録・空なら `null`）

- [x] **Step 1: 失敗するテストを書く**

`Client.Tests/Map/NearestSearch/NearestTargetIndexTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     guid別索引の分離と上書き再構築を検証
    ///     Verifies per-guid separation and overwrite-rebuild of the index
    /// </summary>
    public class NearestTargetIndexTest
    {
        private static readonly Guid TreeGuid = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid RockGuid = new("00000000-0000-0000-0000-000000000002");

        [Test]
        public void 別guidの近い個体は返さない()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var farTree = new NearestSearchTestTarget(new Vector3(100f, 0f, 0f));
            var nearRock = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            index.SetTargets(TreeGuid, new[] { farTree });
            index.SetTargets(RockGuid, new[] { nearRock });

            Assert.AreSame(farTree, index.SearchNearest(TreeGuid, Vector3.zero));
            Assert.AreSame(nearRock, index.SearchNearest(RockGuid, Vector3.zero));
        }

        [Test]
        public void 未登録guidと空リストはnullを返す()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            Assert.IsNull(index.SearchNearest(TreeGuid, Vector3.zero));

            index.SetTargets(TreeGuid, new List<NearestSearchTestTarget>());
            Assert.IsNull(index.SearchNearest(TreeGuid, Vector3.zero));
        }

        [Test]
        public void 同じguidへのSetTargetsは前の点集合を置き換える()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var first = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            var second = new NearestSearchTestTarget(new Vector3(50f, 0f, 0f));
            index.SetTargets(TreeGuid, new[] { first, second });
            Assert.AreSame(first, index.SearchNearest(TreeGuid, Vector3.zero));

            index.SetTargets(TreeGuid, new[] { second });
            Assert.AreSame(second, index.SearchNearest(TreeGuid, Vector3.zero));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗（型未定義）を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `NearestTargetIndex` が見つからないエラー

- [x] **Step 3: 実装する**

`Client.Game/InGame/Map/NearestSearch/NearestTargetIndex.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     key（mapObjectGuid・veinGuid等）別に独立したk-d treeを持つ最寄り索引。点集合の差し替えはkey単位の再構築
    ///     Nearest index holding one independent k-d tree per key (mapObjectGuid, veinGuid, ...); replacing a set rebuilds that key only
    /// </summary>
    public sealed class NearestTargetIndex<T> where T : class, INearestSearchTarget
    {
        private readonly Dictionary<Guid, KdTree<T>> _treesByKey = new();

        public void SetTargets(Guid key, IReadOnlyList<T> targets)
        {
            _treesByKey[key] = new KdTree<T>(targets);
        }

        public T SearchNearest(Guid key, Vector3 position)
        {
            return _treesByKey.TryGetValue(key, out var tree) ? tree.SearchNearest(position) : null;
        }
    }
}
```

- [x] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "NearestTargetIndexTest|KdTreeTest"`
Expected: 9件 PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/NearestSearch moorestech_client/Assets/Scripts/Client.Tests/Map/NearestSearch
git commit -m "feat(nearest-search): guid別k-d treeを束ねるNearestTargetIndexを追加"
```

---

### Task 3: `MapObjectGameObject` — パース済みGuid保持と `Position` 実装

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs:20-32,81-88,163-166`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs:74`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningAimTest.cs:110`

**Interfaces:**
- Produces: `MapObjectGameObject : MonoBehaviour, IMiningTargetObject, INearestSearchTarget` — `public Guid MapObjectGuid { get; }`（`SetRuntimeIdentity` で1回パース）／`public Vector3 Position => transform.position`（`GetPosition()` は削除）

- [x] **Step 1: `MapObjectGameObject` を変更する**

クラス宣言・フィールド・プロパティ（`MapObjectGameObject.cs` 先頭部）:
```csharp
using Client.Game.InGame.Map.NearestSearch;
// ...既存using...

    public class MapObjectGameObject : MonoBehaviour, IMiningTargetObject, INearestSearchTarget
    {
        [SerializeField] private GameObject outlineObject;
        [SerializeField] private MapObjectHpBarView hpBarView;
        [SerializeField] private int instanceId;
        [SerializeField] private string mapObjectGuid;

        // ツール不要の対象では推奨ツールが空になるため、毎回の確保を避けて共有する
        // Targets that need no tool return an empty recommendation, so share one instance instead of allocating
        private static readonly List<ItemId> EmptyToolItemIds = new();

        // 最寄り探索が毎フレーム比較するため、文字列guidは注入時に1回だけパースして保持する
        // Nearest search compares this every frame, so parse the string guid once at injection and keep it
        private Guid _mapObjectGuid;

        public bool IsDestroyed { get; private set; }
        public int CurrentHp { get; private set; }

        public int InstanceId => instanceId;
        public Guid MapObjectGuid => _mapObjectGuid;
        public MapObjectMasterElement MapObjectMasterElement { get; private set; }
        public GameObject GameObject => gameObject;
        public Vector3 Position => transform.position;
```

`SetRuntimeIdentity`:
```csharp
        // 実行時Instantiate用にID/GUIDを注入する（ベイク時代のSerializeField直接参照の置換）
        // Injects identity for runtime instantiation (replaces baked SerializeField values)
        public void SetRuntimeIdentity(int instanceId, string mapObjectGuid)
        {
            this.instanceId = instanceId;
            this.mapObjectGuid = mapObjectGuid;
            _mapObjectGuid = new Guid(mapObjectGuid);
        }
```

`GetPosition()` メソッド（163-166行）は削除する。

- [x] **Step 2: 呼び出し側を `Position` へ置き換える**

`MapObjectPin.cs:74`: `transform.position = mapObject.GetPosition();` → `transform.position = mapObject.Position;`
`MiningAimTest.cs:110`: `_playerObject.transform.position = expectedMapObject.GetPosition();` → `_playerObject.transform.position = expectedMapObject.Position;`
（`MapObjectGameObjectDatastore.cs:188` の `GetPosition()` はTask 4で本体ごと消えるが、このタスクのコンパイルを通すため一旦 `mapObject.Position` に置き換える）

- [x] **Step 3: コンパイルと既存テストで回帰が無いことを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectHpBarScaleTest|MiningAimTest"`
Expected: 全件 PASS

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningAimTest.cs
git commit -m "refactor(map-object): MapObjectGuidをパース済み保持にしPositionでINearestSearchTargetを実装"
```

---

### Task 4: `MapObjectNearestSearcher`（dirty→生存個体で再構築）とDatastoreの載せ替え

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectNearestSearcher.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs:27-28,104-111,147-163,178-197`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectNearestSearcherTest.cs`

**Interfaces:**
- Consumes: Task 2 `NearestTargetIndex<MapObjectGameObject>`, Task 3 `MapObjectGameObject.MapObjectGuid` / `.Position` / `.IsAvailable`
- Produces: `public sealed class MapObjectNearestSearcher` — `public void Register(MapObjectGameObject mapObject)`（guid別リストへ追加しそのguidをdirty）／`public void MarkDirty(Guid mapObjectGuid)`／`public MapObjectGameObject SearchNearest(Guid mapObjectGuid, Vector3 position)`（dirtyなら `IsAvailable` な個体だけで再構築してから探索。該当なしは `null`）
- Produces: `MapObjectGameObjectDatastore.SearchNearestMapObject(Guid, Vector3)` はシグネチャ不変で内部が `_nearestSearcher.SearchNearest` へ委譲

- [x] **Step 1: 失敗するテストを書く**

`Client.Tests/Map/MapObjectNearestSearcherTest.cs`（`MapObjectHpBarScaleTest` と同じくサーバDI＋実在guidで実体を組む）:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     破壊済み個体の除外（dirty→再構築）とguid分離を実体のMapObjectGameObjectで検証
    ///     Verifies destroyed-object exclusion (dirty→rebuild) and guid separation with real MapObjectGameObjects
    /// </summary>
    public class MapObjectNearestSearcherTest
    {
        // ForUnitTestに実在するmapObjectのguid（MapObjectHpBarScaleTestと同じ）
        // A mapObject guid that exists in ForUnitTest (same as MapObjectHpBarScaleTest)
        private static readonly Guid ExistingMapObjectGuid = new("00000000-0000-2222-0000-000000000001");
        // 同じくForUnitTestのmap.jsonに実在する別guid（guid分離の検証用）
        // Another guid that exists in ForUnitTest map.json (for guid-separation checks)
        private static readonly Guid OtherMapObjectGuid = new("00000000-0000-1111-0000-000000000001");

        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created) UnityEngine.Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void 破壊済み個体は次の探索から返らない()
        {
            var searcher = new MapObjectNearestSearcher();
            var near = CreateMapObject(1, ExistingMapObjectGuid, new Vector3(1f, 0f, 0f), false);
            var far = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(near);
            searcher.Register(far);
            Assert.AreSame(near, searcher.SearchNearest(ExistingMapObjectGuid, Vector3.zero));

            near.DestroyMapObject();
            searcher.MarkDirty(ExistingMapObjectGuid);
            Assert.AreSame(far, searcher.SearchNearest(ExistingMapObjectGuid, Vector3.zero));

            far.DestroyMapObject();
            searcher.MarkDirty(ExistingMapObjectGuid);
            Assert.IsNull(searcher.SearchNearest(ExistingMapObjectGuid, Vector3.zero));
        }

        [Test]
        public void 初期スナップショットで破壊済みの個体は最初から返らない()
        {
            var searcher = new MapObjectNearestSearcher();
            var destroyedAtStart = CreateMapObject(1, ExistingMapObjectGuid, new Vector3(1f, 0f, 0f), true);
            var alive = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(destroyedAtStart);
            searcher.Register(alive);
            Assert.AreSame(alive, searcher.SearchNearest(ExistingMapObjectGuid, Vector3.zero));
        }

        [Test]
        public void 別guidの近い個体は返さない()
        {
            var searcher = new MapObjectNearestSearcher();
            var nearOther = CreateMapObject(1, OtherMapObjectGuid, new Vector3(1f, 0f, 0f), false);
            var farTarget = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(nearOther);
            searcher.Register(farTarget);
            Assert.AreSame(farTarget, searcher.SearchNearest(ExistingMapObjectGuid, Vector3.zero));
        }

        [Test]
        public void 未登録guidはnullを返す()
        {
            var searcher = new MapObjectNearestSearcher();
            Assert.IsNull(searcher.SearchNearest(ExistingMapObjectGuid, Vector3.zero));
        }

        private MapObjectGameObject CreateMapObject(int instanceId, Guid guid, Vector3 position, bool isDestroyed)
        {
            var gameObject = new GameObject($"MapObject_{instanceId}");
            gameObject.transform.position = position;
            _created.Add(gameObject);
            var mapObject = gameObject.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(instanceId, guid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(instanceId, isDestroyed, 1));
            return mapObject;
        }
    }
}
```
注: 2つのguidはどちらも `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json` に実在する（マスタ欠落だと `Initialize` が `LogError` を出し `IsAvailable=false` になってテストの意図が崩れるため、実在guidであることが前提）。

- [x] **Step 2: コンパイルして失敗（型未定義）を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MapObjectNearestSearcher` が見つからないエラー

- [x] **Step 3: `MapObjectNearestSearcher` を実装する**

`Client.Game/InGame/Map/MapObject/MapObjectNearestSearcher.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectGuid別の最寄り探索。破壊はdirtyで受け、次の探索時に生存個体だけで索引を組み直す
    ///     Nearest search per mapObjectGuid; destruction marks the guid dirty and the next search rebuilds the index from live objects only
    /// </summary>
    public sealed class MapObjectNearestSearcher
    {
        private readonly Dictionary<Guid, List<MapObjectGameObject>> _mapObjectsByGuid = new();
        private readonly HashSet<Guid> _dirtyGuids = new();
        private readonly NearestTargetIndex<MapObjectGameObject> _nearestIndex = new();

        // 再構築時の生存個体バッファ。索引側が配列へ複製するので使い回せる
        // Live-object buffer for rebuilds; the index copies into its own arrays, so this can be reused
        private readonly List<MapObjectGameObject> _availableBuffer = new();

        public void Register(MapObjectGameObject mapObject)
        {
            var guid = mapObject.MapObjectGuid;
            if (!_mapObjectsByGuid.TryGetValue(guid, out var mapObjects))
            {
                mapObjects = new List<MapObjectGameObject>();
                _mapObjectsByGuid.Add(guid, mapObjects);
            }

            // 生成はフレーム分散なので登録のたびにdirtyにし、最初の探索で一括構築する
            // Instantiation is spread across frames, so mark dirty per registration and build once on the first search
            mapObjects.Add(mapObject);
            _dirtyGuids.Add(guid);
        }

        public void MarkDirty(Guid mapObjectGuid)
        {
            _dirtyGuids.Add(mapObjectGuid);
        }

        public MapObjectGameObject SearchNearest(Guid mapObjectGuid, Vector3 position)
        {
            if (_dirtyGuids.Remove(mapObjectGuid)) RebuildIndex(mapObjectGuid);
            return _nearestIndex.SearchNearest(mapObjectGuid, position);
        }

        private void RebuildIndex(Guid mapObjectGuid)
        {
            // 可否の判断はここで行い、索引には生存個体の座標だけを渡す
            // Availability is decided here; the index receives only live objects' positions
            _availableBuffer.Clear();
            foreach (var mapObject in _mapObjectsByGuid[mapObjectGuid])
            {
                if (mapObject.IsAvailable) _availableBuffer.Add(mapObject);
            }

            _nearestIndex.SetTargets(mapObjectGuid, _availableBuffer);
        }
    }
}
```

- [x] **Step 4: `MapObjectGameObjectDatastore` を載せ替える**

フィールド（27-28行付近）:
```csharp
        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        private readonly Dictionary<Guid, GameObject> _prefabCacheByMapObjectGuid = new();
        private readonly MapObjectNearestSearcher _nearestSearcher = new();
```

生成ループの `TryAdd` 成功後（104-111行付近）:
```csharp
                    mapObject.SetRuntimeIdentity(layout.InstanceId, layout.MapObjectGuid);
                    if (!_allMapObjects.TryAdd(layout.InstanceId, mapObject))
                    {
                        Debug.LogError($"MapObject duplicate InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                        Destroy(instance);
                        continue;
                    }

                    // 登録後にスナップショットで初期状態（破壊/HP）を適用する
                    // Apply the initial state (destroy/HP) from the snapshot after registration
                    mapObject.Initialize(snapshot);

                    // 最寄り探索の候補へ登録する（初期破壊済みは探索時の生存フィルタで除かれる）
                    // Register as a nearest-search candidate (ones destroyed in the snapshot drop out at the live filter on search)
                    _nearestSearcher.Register(mapObject);
```

`OnUpdateMapObject` のDestroy分岐（150-152行付近）:
```csharp
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    mapObject.DestroyMapObject();
                    // 破壊は索引へ即時反映せず、次の探索で該当guidだけ再構築する
                    // Destruction isn't applied to the index immediately; the next search rebuilds just this guid
                    _nearestSearcher.MarkDirty(mapObject.MapObjectGuid);
                    break;
```

`SearchNearestMapObject`（178-197行）を丸ごと置き換え:
```csharp
        public MapObjectGameObject SearchNearestMapObject(Guid mapObjectGuid, Vector3 position)
        {
            return _nearestSearcher.SearchNearest(mapObjectGuid, position);
        }
```
`using System.Linq;` は `ToDictionary` で引き続き使うため残す。

- [x] **Step 5: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`MapObjectGameObjectDatastore.cs` が200行以下であることも `wc -l` で確認）
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectNearestSearcherTest|MapObjectHpBarScaleTest|MiningAimTest"`
Expected: 全件 PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectNearestSearcherTest.cs*
git commit -m "feat(map-object): 最寄りmapObject探索をk-d tree索引に載せ替え（破壊はdirty→次探索で再構築）"
```

---

### Task 5: 露頭側の載せ替えと `OutcropGuidIndex` 廃止

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs:16-27`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs:31,44-63,101-115,142-145`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGuidIndex.cs`（`.meta` も一緒に `git rm`）

**Interfaces:**
- Consumes: Task 2 `NearestTargetIndex<OutcropGameObject>`
- Produces: `OutcropGameObject : MonoBehaviour, IMiningTargetObject, INearestSearchTarget` — `public Vector3 Position => transform.position`
- Produces: `OutcropGameObjectDatastore.SearchNearestOutcrop(Guid, Vector3)` はシグネチャ不変

- [x] **Step 1: `OutcropGameObject` に `INearestSearchTarget` を実装する**

```csharp
using Client.Game.InGame.Map.NearestSearch;
// ...既存using...

    public class OutcropGameObject : MonoBehaviour, IMiningTargetObject, INearestSearchTarget
    {
        // ...既存フィールド...

        public GameObject GameObject => gameObject;
        public Vector3 Position => transform.position;
        public SoundEffectType DestroySoundType { get; private set; }
```

- [x] **Step 2: Datastoreを載せ替える**

フィールド（31行）:
```csharp
        private readonly Dictionary<string, GameObject> _prefabCacheByAddress = new();

        // 露頭は破壊されないので生成完了時に1回だけ索引を組む
        // Outcrops are never destroyed, so the index is built once when instantiation completes
        private readonly Dictionary<Guid, List<OutcropGameObject>> _outcropsByVeinGuid = new();
        private readonly NearestTargetIndex<OutcropGameObject> _nearestIndex = new();
```

`InstantiateOutcropsFromLayoutAsync` のループ終了後（63行の `}` の直後、`#region Internal` の前ではなくローカル関数本体の末尾）:
```csharp
                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }

                // 全露頭が出揃ってから鉱脈ごとに索引を焼く
                // Bake one index per vein once every outcrop exists
                foreach (var pair in _outcropsByVeinGuid) _nearestIndex.SetTargets(pair.Key, pair.Value);
            }
```

`InstantiateOutcrop` 内（`_outcropGuidIndex.Add(veinGuid, outcrop);` の置き換え）:
```csharp
                var outcrop = instance.GetComponent<OutcropGameObject>();
                if (outcrop == null) outcrop = instance.AddComponent<OutcropGameObject>();
                if (!_outcropsByVeinGuid.TryGetValue(veinGuid, out var outcrops))
                {
                    outcrops = new List<OutcropGameObject>();
                    _outcropsByVeinGuid.Add(veinGuid, outcrops);
                }
                outcrops.Add(outcrop);
```

`SearchNearestOutcrop`:
```csharp
        public OutcropGameObject SearchNearestOutcrop(Guid veinGuid, Vector3 position)
        {
            return _nearestIndex.SearchNearest(veinGuid, position);
        }
```
`using Client.Game.InGame.Map.NearestSearch;` を追加。

- [x] **Step 3: `OutcropGuidIndex` を削除する**

```bash
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGuidIndex.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGuidIndex.cs.meta
grep -rn "OutcropGuidIndex" moorestech_client/Assets/Scripts   # 0件であること
```

- [x] **Step 4: コンパイルと既存テストで回帰が無いことを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Outcrop|Vein"`
Expected: 全件 PASS（`IgnoreCI` カテゴリは環境により skip 可）

- [x] **Step 5: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop
git commit -m "refactor(outcrop): 最寄り露頭探索をNearestTargetIndexへ載せ替えOutcropGuidIndexを廃止"
```

---

### Task 6: ピンの死コード削除とLogErrorの1回化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs:1-9,18-20,28-33,35-78`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/VeinPin.cs:1-9,19-21,33-38,40-48`

**Interfaces:**
- Consumes: `MapObjectGameObjectDatastore.SearchNearestMapObject` / `OutcropGameObjectDatastore.SearchNearestOutcrop`（不変）
- Produces: `MapObjectPin.Construct(MapObjectGameObjectDatastore)` / `VeinPin.Initialize(OutcropGameObjectDatastore)`（`InGameCameraController` 依存を除去。VContainerの `[Inject]` メソッドなので登録側の変更は不要）

- [x] **Step 1: `MapObjectPin` を変更する**

- `using Client.Game.InGame.Control;` とフィールド `_inGameCameraController` を削除、`Construct` から `InGameCameraController` 引数を外す
- `Update` 冒頭の `transform.LookAt(...)`／`transform.rotation = Quaternion.Euler(...)` の2行とそのコメントを削除（ピンprefabにRendererが無く向きは無意味）
- 対象不在ログの1回化。フィールドを追加し `NearestPinMapObject` を置き換える:

```csharp
        // 対象不在は毎フレーム出すとログを埋めるので、対象1件につき1回だけ報告する（VeinPinと同形）
        // Reporting a missing target every frame would bury the log, so report once per target (same as VeinPin)
        private Guid _reportedMissingMapObjectGuid;
```
```csharp
            void NearestPinMapObject()
            {
                // 近くのMapObjectを探してピンを表示
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_currentTutorialParam.MapObjectGuid, playerPos);

                if (mapObject == null)
                {
                    if (_reportedMissingMapObjectGuid != _currentTutorialParam.MapObjectGuid)
                    {
                        _reportedMissingMapObjectGuid = _currentTutorialParam.MapObjectGuid;
                        Debug.LogError($"未破壊のMapObject {_currentTutorialParam.MapObjectGuid} が存在しません");
                    }
                    return;
                }

                transform.position = mapObject.Position;
            }
```
（`using System;` を追加）

- [x] **Step 2: `VeinPin` を変更する**

- `using Client.Game.InGame.Control;` とフィールド `_inGameCameraController` を削除、`Initialize` から `InGameCameraController` 引数を外す
- `Update` 内の `transform.LookAt(...)`／`transform.rotation = Quaternion.Euler(...)` の2行とそのコメントを削除

- [x] **Step 3: コンパイルとTutorial系テストで回帰が無いことを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `grep -rn "InGameCameraController" moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/VeinPin.cs`
Expected: 0件
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Tutorial"`
Expected: 全件 PASS

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/VeinPin.cs
git commit -m "refactor(tutorial-pin): 死コードのLookAtを削除しmapObject不在ログを対象ごと1回にする"
```

---

### Task 7: `WorldPinStateStore` の毎フレームアロケーション除去

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/WorldPinStateStore.cs:1-5,37-70,78-94`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WorldPinStateStoreTest.cs`（既存・変更なし・回帰確認に使う）

**Interfaces:**
- Consumes/Produces: 公開API不変（`SetPin`/`RemovePin`/`GetCurrent`/`ObserveChanged`）

- [x] **Step 1: `SetPin` の `FirstOrDefault` をループに置き換える**

```csharp
        public void SetPin(string pinId, string tutorialGuid, WorldPinProjection projection)
        {
            var existing = FindPin(pinId);
            if (existing != null && IsSame(existing)) return;

            if (existing == null)
            {
                existing = new WorldPinData { PinId = pinId };
                _pins.Add(existing);
            }

            existing.TutorialGuid = tutorialGuid;
            existing.ScreenX = projection.ScreenX;
            existing.ScreenY = projection.ScreenY;
            existing.OnScreen = projection.OnScreen;
            existing.DirectionX = projection.DirectionX;
            existing.DirectionY = projection.DirectionY;
            Publish();

            #region Internal

            WorldPinData FindPin(string targetPinId)
            {
                // 毎フレーム呼ばれるのでLINQのクロージャ確保を避ける
                // Called every frame, so avoid the LINQ closure allocation
                foreach (var pin in _pins)
                {
                    if (pin.PinId == targetPinId) return pin;
                }

                return null;
            }

            bool IsSame(WorldPinData pin)
            {
                // （既存のまま）
            }

            #endregion
        }
```

- [x] **Step 2: `CreateData` の `Select().ToArray()` をループに置き換える**

```csharp
        private WorldPinPresentationData CreateData()
        {
            // 配信1回につき配列1本。Selectのイテレータ確保を避ける
            // One array per publish; avoid the Select iterator allocation
            var pins = new WorldPinData[_pins.Count];
            for (var i = 0; i < _pins.Count; i++)
            {
                var pin = _pins[i];
                pins[i] = new WorldPinData
                {
                    PinId = pin.PinId,
                    TutorialGuid = pin.TutorialGuid,
                    ScreenX = pin.ScreenX,
                    ScreenY = pin.ScreenY,
                    OnScreen = pin.OnScreen,
                    DirectionX = pin.DirectionX,
                    DirectionY = pin.DirectionY,
                };
            }

            return new WorldPinPresentationData { Revision = _revision, Pins = pins };
        }
```
`using System.Linq;` は `RemovePin` の `RemoveAll`（LINQではなく`List<T>`のメソッド）しか残らないので削除する。

- [x] **Step 3: コンパイルと既存テストで回帰が無いことを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "WorldPinStateStoreTest"`
Expected: 全件 PASS

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/WorldPinStateStore.cs
git commit -m "perf(world-pin): WorldPinStateStoreの毎フレームLINQアロケーションを除去"
```

---

### Task 8: 通し検証（PlayMode）とbd更新

**Files:**
- 変更なし（検証のみ）

- [ ] **Step 1: 木チュートリアルの通し動作を確認する** — **ブロック（本PR外の既知不整合）**

> masterデータとスキーマの不整合でPlayModeが起動できず未実施。`VanillaSchema/map.yml` の `mapObjects[].terrainSurroundEffectType`（enum必須）は本repoの `430925007`(2026-08-17、本ブランチのbaseに含まれる)で入ったが、master data側は `8fcefa5` で `mapVeins` にしか追加しておらず、mapObjects側を満たすmasterコミットが存在しない。実測: `ab9e8bc4`(本ブランチのピン)=`fluids[0].color` 欠落 / `e15995ab`=`entries[0].placementMode` 欠落 / `00dda1f8`・`6e01345`(現origin/masterのピン)=`mapObjects[0].terrainSurroundEffectType` 欠落。既存bd `moorestech-lft8`(P0)・`moorestech-hvwb`(P0)・`moorestech-n2xv` と同一事象。シナリオ `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-tree-pin-nearest-search.cs` は作成済みで、環境復旧後にそのまま実行できる。代替として破壊イベント→`MarkDirty`→索引脱落の経路は `MapObjectRotationTest`(EditModeInPlayingTest・実データ起動)で検証済み


unity-playmode-recorded-playtest スキル（プレイテストDSL）で、木ピンのチュートリアルが出る段階まで進め、(a) ピンが最寄りの木を指す (b) 1本伐採後にピンが次の木へ移る (c) Errorログに「未破壊のMapObject」「露頭が存在しません」が出ない、を確認する。
Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本変更由来のErrorが0件

- [x] **Step 2: 全EditModeテストの回帰確認**

> 一括フィルタは uloop クライアントの180秒上限で切れるため7チャンクに分割して実行。合計 245 passed / 2 failed。failedの2件(`MapObjectAddressableLoadTest` / `MapObjectRayTargetTest`)はいずれも `Assets/AddressableResources/Environment/Rock/MesaDesert/StratMesaSharp_0.prefab` 不在が原因で、本ブランチは `AddressableResources` 配下を1ファイルも変更していない(既存のmasterデータ↔非公開アセット不整合)


Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Map|Tutorial|Mining|WebUi|NearestSearch"`
Expected: 全件 PASS

- [x] **Step 3: bdを更新する**

```bash
bd update moorestech-8tw6 --claim
bd note moorestech-8tw6 "k-d tree索引化を実装。plan: docs/superpowers/plans/2026-08-23-nearest-search-kd-tree-index.md"
```
（closeはPRマージ後。実測フォローアップは moorestech-rw09 に既存）

---

### Task 9: 全ブランチレビュー（必須・省略不可）

- [x] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキル（moores-code-review）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断はAskUserQuestionで仰ぐ。

- [x] **Step 2: 修正をコミットし pr-create スキルでPRを作る**

```bash
git add -A && git commit -m "fix: moores-code-review指摘を反映"
```
PR本文には `.decisions/2026-08-23-最寄りmapObject探索はk-d treeで索引化する.md` と bd moorestech-8tw6 をリンクする。

---

## 判断記録（ADR）

設計セッションの裁定: `.decisions/2026-08-23-最寄りmapObject探索はk-d treeで索引化する.md`（k-d tree採用・両Datastore適用・dirty→再構築・スコープ）

planning中に新たに生じた判断:

- **dirty管理を `MapObjectNearestSearcher` として切り出す**（出所: agent前提）— `MapObjectGameObjectDatastore` は198行で200行規約に当たるため、探索責務（guid別リスト・dirty・生存フィルタ・索引プッシュ）を同ディレクトリの別クラスへ出す。索引（`NearestTargetIndex`）は可否を知らない裁定を保ちつつ、可否判断の置き場が具体側のクラスとして単体テスト可能になる
- **mapObject側は `Register` のたびにdirty、初回探索で一括構築**（出所: agent前提）— 生成がフレーム分散（100件ごとにYield）で、完了フックを別途設けるより「dirtyなら再構築」の単一経路に乗せる方が状態が1つ減る。初期スナップショット破壊済み（`Initialize`内で`DestroyMapObject`）も同じ生存フィルタで自然に除かれる
- **露頭側は生成完了時に1回構築・dirty経路なし**（出所: 裁定「露頭は破壊されない」）— `OutcropGuidIndex` は廃止し、Datastore直下の `Dictionary<Guid, List>` ＋ `NearestTargetIndex` に畳む（ユーザー指示「OutcropGuidIndex を廃止して同じ索引に載せ替え」）
- **k-d treeは暗黙平衡木（ノード配列を持たない）・構築は区間ソート**（出所: agent前提）— 663〜2000点規模で構築は破壊イベント時のみのため O(n log² n) のソート構築で十分。ノード構造体を持たないぶん実装行数とバグ面が減る。探索は再帰・フィールド最良値でアロケーションゼロ
- **タイブレークは「厳密に近い側のみ更新・等距離は走査順」とし、テストは等距離では距離一致のみ検証**（出所: agent前提）— 現行の2実装もタイ規約が割れている（mapObject側は後勝ち・露頭側は先勝ち）ため、同一個体の保証は要件にしない
- **`MapObjectGameObject.GetPosition()` を `Position` プロパティに置換**（出所: agent前提）— `INearestSearchTarget.Position` と二重化させない。呼び出し側2箇所（MapObjectPin・MiningAimTest）を更新
- **ピンから `InGameCameraController` 依存を外す**（出所: agent前提）— LookAt削除で唯一の用途が消える。未使用注入を残さない
- **`WorldPinStateStore.RemovePin` の `RemoveAll` はそのまま**（出所: agent前提）— 呼ばれるのは非表示時のみで毎フレーム経路ではない。裁定スコープ（SetPin・CreateData）に限定

---

planが完成し `docs/superpowers/plans/2026-08-23-nearest-search-kd-tree-index.md` に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-08-23-nearest-search-kd-tree-index.md
- 作業場所: feature/nearest-search-kd-tree（`moores-wt new feature/nearest-search-kd-tree` で作ったworktree）
- plan本体と `.decisions/2026-08-23-最寄りmapObject探索はk-d treeで索引化する.md` はメインワークツリーに未追跡で置いてあるので、worktree作成後にその2ファイルをコピーして最初にコミットしてください
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 進捗はplanのチェックボックス更新で管理してください
- planの最終タスク（コードレビュースキルによる全ブランチレビュー）は省略不可です
```
