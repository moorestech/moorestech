決定: MapObjects に必須化した6キー（scaleX/Y/Z・clusterId・clusterCenterX/Z）の `../moorestech_master` への投入は `server_v8/map/map.json`（2002件）だけに絞る。v4〜v7 は未更新のまま残す。moorestech_master は detached HEAD かつ全checkout共有なので、専用ブランチ `feat/mapobject-scale-cluster-keys` を切ってコミットする。本ブランチは新データを要求するので `.moorestech-external-revisions.json` のpinは動かす（動かさない選択肢は無い）。共有checkout `/Users/katsumi/moorestech_master` は旧pin `c610e13` の detached HEAD に戻し、新pinは専用worktree `/Users/katsumi/moorestech-master-worktrees/mapobject-scale-cluster-keys` が持つ。プレイテストのpin解決（`unity-playmode-recorded-playtest/scripts/preflight.sh:26-27`）は「HEADがpin一致のworktree」を探すだけなので、旧pinのworktreeを残さないと他checkoutが起動不能になる。

棄却案: server_v4〜v8 の全 map.json を一括更新する案（AGENTS.md「変更の波及を恐れない・全JSON一括更新」の正道）。コード側に「キー欠損を検出して明示エラー」を入れて master は触らない案。

理由: テストプレイで実際にロードされるのは server_v8 のみで、v4〜v7 はレガシー。キー追加は Newtonsoft が未知キーを無視するため他ブランチの読み取りを壊さない一方、更新対象を広げるほど共有repoでの衝突面が増える。明示エラー案は v8 ワールドを起動不能にはしないが、v4〜v7 を開くと今度はハードフェイルになるため、Critical の解消には JSON 側の投入が要る。v4〜v7 を開いたときに scale=0・clusterId=0（int既定値で -1 ではない）へ落ちる残存リスクは bd へ積んで追跡する。

リンク: Task 7 レビュー Critical-1 / AGENTS.md 設計原則（必須キー化・フォールバック禁止） / bd moorestech-edd
