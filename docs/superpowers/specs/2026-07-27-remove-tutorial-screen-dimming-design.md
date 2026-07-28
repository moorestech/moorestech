# チュートリアル画面暗転の撤去設計

## 目的

DOMアンカーを示すチュートリアル中も、対象外のゲーム画面とUIを通常の明るさで表示する。対象DOMの位置を示す黄色い輪郭、DOM追従、callout、アンカー解決通知は維持する。

## 現状

Web UIのチュートリアル表示は、`spotlight` 種別の輪郭へ非常に大きい半透明の `box-shadow` を付け、輪郭の外側を全面暗転している。対象DOM自体をオーバーレイから切り抜く実装ではないため、対象を含む画面全体が暗く見える。

`spotlight` の生成元はUnity側のUI・アイテム用チュートリアルmanagerの2箇所に限られる。Web側では表示契約とCSSがこの種別を保持している。

## 設計

暗転を表す `spotlight` 種別をUnity producer、Webの表示契約、CSSから削除する。既存の2つのproducerは用途別API `AddOutlineHighlight(anchorId, message)` を呼び、チュートリアル対象を黄色い輪郭だけで示す。

state storeのpublic APIから任意の `kind` 文字列引数を除去し、DTOへ設定する `"outline"` はstore内部へ閉じ込める。未使用のcallout producer APIは追加しない。これにより旧 `spotlight` とtypoをUnity側で表現不能にし、Web側でpresentation全体が契約違反になる経路をなくす。

`callout` はメッセージ付き輪郭として維持する。DOMアンカーの購読、位置とpaddingの反映、Unityへのanchor ack、pointer inputの扱いは変更しない。

CSSだけを無効化する案は採用しない。契約に不要な `spotlight` が残ると、将来のproducerが暗転を意図して再利用でき、撤去済みの機能と実装可能な契約が矛盾するためである。

## データフロー

1. Unityのチュートリアルmanagerが `AddOutlineHighlight` でhighlightをstate storeへ追加する。
2. Web hostが既存の `tutorial.presentation` topicでそのhighlightを配信する。
3. Web UIがDOMアンカーを解決し、対象矩形の周囲へ黄色い輪郭を描画する。
4. 解決状態は既存どおりUnityへackされる。

## エラー処理

アンカー未検出・非表示・重複時の既存挙動は変更しない。Web契約へ旧 `spotlight` が届いた場合は、暗黙に `outline` へ変換せず契約違反として拒否する。

## 最も強い反例

複数の `outline` と `callout` が同時に配信される場合でも、各輪郭は独立してDOM矩形へ追従し、巨大なshadowを持たないため画面を暗くしない。旧 `spotlight` が混在する入力は契約で拒否し、部分的な暗転再発を防ぐ。

## テスト

- Web契約テストで `outline` と `callout` を受理し、`spotlight` を拒否する。
- Unity state storeテストで用途別APIがwire出力へ `outline` を設定することを確認する。2つのproducer移行はコンパイルと残存参照検索で担保する。
- Web UIの回帰テストで実DOMへ `outline` presentationを配信し、黄色い輪郭が表示され、巨大な黒いshadowがないことをcomputed styleで確認する。
- 暗転用CSSと `spotlight` 参照が残っていないことを静的検索する。
- Web側テスト、対象Unityテスト、Unityコンパイルを実行する。

## 判断記録（ADR）

- ユーザー裁定（発言「ok」、2026-07-27）: 暗転機能を契約ごと削除し、既存チュートリアルは黄色い `outline` へ移行する。
- agent前提（不要機能を契約から除去する原則、拒否権つき）: DOM追従、callout、anchor ack、pointer inputの挙動は変更対象外とする。
- agent前提（型で排除する原則、拒否権つき）: state storeの任意kind引数を用途別 `AddOutlineHighlight` へ置換し、旧種別とtypoをUnity側でも表現不能にする。
- 機構比較: CSSのみの無効化ではなく、Unity producer・Web契約・CSSを一括更新して `spotlight` を表現不能にする。
