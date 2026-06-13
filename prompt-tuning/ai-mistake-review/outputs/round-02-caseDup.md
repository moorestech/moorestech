# レビュー結果 — caseDup

Warning | FluidMapVeinGameObject.cs:34-91 | D | 既存のアイテム鉱脈用 MapVeinGameObject（MinPosition/MaxPosition/SetBounds/Update/OnDrawGizmosSelected）をほぼ丸ごとコピペして色だけ青に変えた重複クラス。人間は共通の VeinGameObject 基底/サービスへ Bounds・座標計算・Gizmo描画を抽出し、色とFluidGuidだけを差分にする。
Warning | FluidMapVeinGameObjectInspector.cs:1-34 | D | 既存の MapVeinGameObjectInspector とほぼ同一（BoxBoundsHandle 配置・EndChangeCheck・SetBounds・SetDirty）を色だけ変えてコピペ。人間は基底Inspectorまたは共通ヘルパへ抽出して重複解消する。
Warning | FluidMapVeinDatastore.cs:216-218 | V | 「既存map.jsonとの互換のためnull許容」コメント付きで FluidVeins==null 早期return を追加。本プロジェクトは後方互換不要。人間はこの互換ガードとコメントを削除し、MapInfoJson 側で FluidVeins を空リスト初期化する。
Warning | FluidMapVeinGameObjectInspector.cs:129-131 | S | SetBounds でオブジェクトを変更した「後」に Undo.RecordObject を呼んでおり、Undo が変更前状態を記録できずアンドゥが効かない。人間は SetBounds 実行前に Undo.RecordObject を移動する。
Nit | FluidMapVeinDatastore.cs:234-244 | P | GetOverVeins が呼び出しごとに全Veinを線形スキャン。ポンプが毎tick設置位置で呼ぶなら重い。人間は設置時に1回引いてキャッシュする、または件数が少ない前提なら許容と判断。
Nit | FluidMapVeinGameObject.cs:46 | P | VeinFluidGuid => Guid.Parse(veinFluidGuid) がアクセスのたびにパース。頻繁参照なら一度パースしてキャッシュする。
Nit | FluidMapVeinGameObject.cs:46-50 | V | VeinFluidGuid / Bounds を getter-onlyプロパティで公開しているが、規約上は値Setを SetHoge に寄せる方針。Bounds は SetBounds があり整合、VeinFluidGuid は読み取り専用変換なので許容範囲だが要確認。

最有力の1件: **FluidMapVeinGameObject.cs:34-91 (D) のアイテム鉱脈クラス丸ごとコピペ重複**。Inspectorも含め2クラス分の重複を生んでおり、人間が最初に共通化で手直しする可能性が最も高い。
