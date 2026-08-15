決定: Task 13 で generate系フラグ（generateHeightmap / generateTexture / generateDetail）のゲートを仕様どおり復元したうえで、`../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json:460` の `generateDetail` を `false` → `true` へ変更する。master 側の変更は Task 7 と同じブランチ `feat/mapobject-scale-cluster-keys`（専用worktree `/Users/katsumi/moorestech-master-worktrees/mapobject-scale-cluster-keys`）に載せ、`.moorestech-external-revisions.json` の pin を更新する。

棄却案: マスタを `false` のまま残し、ゲート復元の結果として detail が消えるのを移植元セマンティクスどおりの正常挙動として受け入れる案。generateDetail のゲートだけ実装せず、heightmap / texture のゲートだけ復元して detail は常時ONのまま残す案。

理由: 現在は R9 のゲート自体が未実装なので detail は常に構築されており、草は実際に表示されている。ゲートだけを忠実に復元するとマスタ値 false が効いて**現在見えている見た目が消える**という、移植漏れの復元がユーザーに見える退行として現れる形になる。加えて Task 9 で復元した Detail 距離フィールド（SDF）は detail が構築されて初めて観測できるため、false のままだと Task 15 の5x5録画で検証できず、Task 9 の成果が無検証で出荷される。マスタ値を true にすれば、ゲートは仕様どおり効きつつ現在の挙動が保たれ、Task 9 の成果も実機で確認できる。ゲートだけ部分実装する案は R9 を中途半端に残し「どのフラグが効くか」が読み手に分からなくなるので採らない。

リンク: Task 13 / plan R9 / Task 9（Detail距離フィールド）/ Task 15（5x5録画検証）/ [[2026-08-15-MapObjects新必須キーの一括投入はserver_v8のみに絞る]] / bd moorestech-edd
