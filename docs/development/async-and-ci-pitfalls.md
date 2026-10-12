# UniTask と CI バッチモードの落とし穴

単体テストとコンパイルは通るのに、起動経路の実挙動や CI でだけ壊れる非同期まわりの規則。

## UniTask.Preserve() は同時 awaiter を支えない
- `Preserve()` がメモ化するのは **完了後の再 await** だけ。未完了の間は内側の promise へ素通しする（`MemoizeSource.OnCompleted`）
- 内側の `UniTaskCompletionSourceCore` は継続を1本しか持てない。未完了中に2本目を登録すると `InvalidOperationException("Already continuation registered, can not await twice or get Status after await.")`
- `Forget()` も継続を1本登録する。同じタスクに `Forget()` と `await` を併用すると必ず例外になる
- 規則: 複数の awaiter が要るなら `.AsTask()` で `System.Threading.Tasks.Task` へ **一度だけ** 変換して保持する。Task は多数の awaiter を支え、例外も全 awaiter へ伝わる
- この種の不具合はローディングが明けない・完了フラグが永久に false のまま、といった起動経路でしか出ない。共有タスクを複数箇所から待つ設計を見たら疑う

## CI（game-ci、batchmode EditMode）で UniTask.RunOnThreadPool が戻らない
- 症状: ローカル Editor では通るテストが、CI で 180 秒タイムアウトする。スレッドプール側の処理は即完了しているのに、復帰だけが止まる
- 原因: `await UniTask.RunOnThreadPool(...)` 末尾の復帰（`UniTask.Yield()`）が Editor の PlayerLoop 更新を待ち、batchmode では戻らない。`[UnityTest]` + `UniTask.ToCoroutine` にしても直らない
- 対処: スレッドプールへ載せる本体を同期メソッド（internal）に切り出し、テストはそれを直接呼ぶ（前例: `CrashBundleWriter.Write`）

## CI 環境を前提にしたテストの書き方
- CI コンテナは root で走るので、`chmod 000` で読み取り失敗を再現するテストは成立しない。root のときは `Assert.Ignore` する
- `Application.isBatchMode` に依存する判定（例: `PlaytestStartGateBypass`）は CI で常に真になる。テストでは判定結果を引数で注入する
