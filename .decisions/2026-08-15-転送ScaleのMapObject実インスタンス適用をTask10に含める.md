決定: Task 7 で転送した Scale を MapObject の実インスタンスへ適用する処理を Task 10 のスコープに含める（`MapObjectGameObjectDatastore.cs:81` の Instantiate に localScale 適用を追加）。計画の R リストには無い項目なので定義上はスコープ拡大にあたるが、R6（岩クラスタ周辺 surround テクスチャ）と同じタスクで入れる。Rotation の復元は今回やらない。

棄却案: Scale 適用を独立タスクとして bd へ積み、Task 10 は R6 だけに絞る案。Rotation も含めてまとめて復元する案（`PlacementEntry.Rotation` はサーバーにあるが `PlacedMapObject` に無く、Task 7 と同規模のワイヤ拡張＋map.json 必須キー追加＋master 再更新が要る）。

理由: surround テクスチャは生成時の Scale で幅を決めるため、岩本体が等倍のままだと「小さい岩の周りに大きな泥」という不整合な見た目になり、Task 15 の録画で R6 が正しく検証できない。適用自体は実質1行で、R6 の成果を見られる状態にするための最小の付随変更にあたる。Rotation は現状すべての岩が同じ方向を向いているという別の移植漏れだが、ワイヤ拡張を伴い規模が違うので分離する。

リンク: Task 10 / plan R6 / Task 7（Scale転送）/ [[2026-08-15-MapObjects新必須キーの一括投入はserver_v8のみに絞る]] / bd moorestech-edd
