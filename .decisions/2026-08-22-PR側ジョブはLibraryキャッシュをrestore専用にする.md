# PR側ジョブはLibraryキャッシュをrestore専用にする

## 決定
`platform-compile.yml` と `run_test.yml` は `actions/cache/restore@v4` を使い、キャッシュの保存を行わない。Libraryキャッシュを焼くのは master 上の `cache-warm.yml` だけとする。

## 棄却した案
- **planどおり `actions/cache@v4`（save付き）のまま残す**: PRごとに server Library 1.17GB × 2ターゲット ≒ 2.34GB を保存するため、PRが数本並ぶだけで10GB枠を超えてLRU退避が始まる。ADR 0028 R10 が想定する「保存は3系統・約6GB」の前提が崩れ、master側で焼いたキャッシュが押し出されて全PRがコールドに戻る。
- **実走の実測を見てから決める**: 枠超過はキャッシュ設計の前提そのものを壊すため、実測を待つ理由が薄い。

## 理由
キャッシュスコープは「自分のref＋ベースブランチ」であり、PR側で焼いたキャッシュは他PRから再利用できない。保存の価値は同一PR内の2回目以降だけで、その利得より master キャッシュを押し出す損失のほうが大きい。

## リンク
- docs/adr/0028-ci-build-strategy.md
- docs/superpowers/plans/2026-08-22-ci-build-strategy-workflows.md (Task 4 / Task 5)
