# 歯車の回転をまとめて進める（2026-11-22、Windows）

歯車の回転（`GearStateChangeProcessor`）は、距離カリングの半径内の 1,893個がそれぞれ `Update` で毎フレーム `Transform.Rotate` していて、1フレーム約4.4ms かかっていた。これを次の3つで減らした。
- **A** 回転角が0（RPM 0 や deltaTime 0）なら Transform に書かない
- **B** 回転方向のワールド符号を、毎フレーム `transform.rotation` から求めるのをやめ、初回に1度だけ求めて持つ
- **C** 歯車ごとの `Update` をやめ、有効な歯車を一覧（`GearRotationUpdateList`）に載せて、`GearRotationTicker`（VContainer の `ITickable`）の1ループで回す

## 結論
- 歯車にかかる時間は、**変更前 約5.5ms → 変更後 約2.9ms**（Profiler、1フレーム）。fps は 13.26 → 13.73。
  - 変更前：`GearStateChangeProcessor.Update` 4.4ms（1,893回）＋ `BehaviourUpdate` の呼び出しの上乗せ約1.1ms
  - 変更後：`VContainerUpdate` が 7.4 → 10.4ms（+2.9ms、歯車の Tick が入った分）、`BehaviourUpdate` が 6.2 → 0.7ms
- 変更後の Tick を Stopwatch で分けると 1.35ms（範囲内の 1,893個を回るだけ 0.56ms、動いている 964個・部品 2,495個を回す 0.88ms、うち `Transform.Rotate` 0.55ms）。Profiler の 2.9ms より小さいのは、同じ処理を50回続けて測っていてキャッシュに乗るためと見ている（推測）。

## 条件
| 項目 | 値 |
|---|---|
| マシン | Windows／i7-12700／RTX 3060 Ti |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、D3D12、`-force-gfx-mt`、コード最適化 Debug、品質 High、Game ビュー 1920×1080 |
| コード | 変更前 `85f0aa1334`（master に調査記録を足しただけ。GRD なし）、変更後はその上にこの変更 |
| ワールド・視点 | ベースライン `2026-10-22-world_1`、`benchmark/snippets/place-player.cs` の位置と視点 |

## 調べたこと（変更前、`snippets/gear-probe.cs`・`gear-nest.cs`）
| 項目 | 値 |
|---|---|
| `GearStateChangeProcessor` | 全体 10,789、半径内で有効 1,893（全部が状態を受信済み） |
| RPM 0 | 929（49%）。止まっていても毎フレーム「0度回す」を書いていた |
| 回す部品 | 半径内で 5,070、全部 Transform 回転（Animator 回転は0）。全ワールドで 28,583 |
| 入れ子（回る部品の祖先にも回る部品） | 0（全ワールド）。B の前提 |
| 1フレーム分の Stopwatch | `Rotate` 全体 2.56ms、うち `Transform.Rotate` 1.02ms・`transform.rotation` の取得 0.50ms |
| 回す部品のルート | 全部が同じ1つのルートの下（`TransformAccessArray` で並列にしても効かない形） |

## 確かめたこと
- テスト（バッチモード、EditMode）：`Client.Tests.Gear` 一式と `GearRotationRegistrationEntryTest` の 37件が通った。足したテストは `GearRotationUpdateListTest` の3件（途中を外しても残りが回る・二重に載せても1回分・RPM 0 は Transform に書かない）。
- ゲームの中（`snippets/gear-live.cs`）：半径内で RPM が 0 より大きい歯車の部品 30個の角度を1秒あけて読み、30個すべてが回っていた（17フレーム、RPM 10〜20）。

## 前提と注意
- B は「ブロックは向きを決めて生成され（`BlockGameObjectPrefabContainer` の `Instantiate(prefab, position, rotation, parent)`）、以後向きが変わらない」「回る部品の入れ子が無い」ことに頼っている。入れ子の部品を足すときは、符号を毎回求める形に戻す必要がある。
- C で、歯車は `OnEnable` で一覧に載り `OnDisable` で外れる。距離カリングは今までどおり `enabled` を切り替えるだけで、半径外の歯車は一覧から外れる。`GearRotationTicker` は `MainGameModelRegistration` で登録しているので、その外（DI の無いシーン）では歯車は回らない。状態を受け取る歯車はメインのゲームの中にしか無い。
- Editor の `GearStateChangeProcessorSimulator` は今までどおり `Rotate` を直接呼ぶので変わらない。

## 再計測
`docs/performance/2026-11-21-draw-call-breakdown/run.sh`（`perf/gpu-resident-drawer` ブランチ）に、`EXTRA=<スニペット>` で読み込み後に1本実行するフックと、`GEAR_PROFILE=1` で fps 10秒と `gear-profile.cs`→`gear-profile-collect.cs` の窓を2回取る処理を足した版で測った（`results/gear-before.txt`・`gear-after.txt`）。

| ファイル | 内容 |
|---|---|
| `snippets/gear-probe.cs` | 変更前の歯車の内訳と、1フレーム分の処理を部分ごとに Stopwatch で測る |
| `snippets/gear-nest.cs` | 回る部品の入れ子の数と、1つの歯車が持つ部品の数 |
| `snippets/gear-probe2.cs` | 変更後の Tick を部分ごとに Stopwatch で測る |
| `snippets/gear-profile.cs` / `gear-profile-collect.cs` | Profiler の記録を始める／歯車・Tick・Update に関係するマーカーを回収する |
| `snippets/gear-live.cs` | ゲームの中で歯車が実際に回っているかを見る |
