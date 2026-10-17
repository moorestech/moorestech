# 0083. 合流マスはファミリーの分岐器モデルでベルト描画を差し替える

日付: 2026-10-17
状態: 採択

## 文脈

ベルトsegment化の仕様たたき台 §1.4 は「合流・分岐マスは箱で隠す（デバッグ段階では隠さなくてよい）」とし、§1.5 で機械入力による合流マスにも同じ方針を適用している。合流マスのアイテムは buffer 内にあり表示されないため覆いが要るが、覆いは未実装で、合流マスは普通の直線ベルコンの見た目のままアイテムだけが消えていた。合流マスはマスタ上のブロックではなく、サーバーが周囲の配置から導く `BeltSegmentKind.Merge`（1マスの segment）で、クライアントは複製の `BeltSegmentShape.Kind` と `Cells[0]` で知る。分岐器ブロックは電気だけが本物のモデル（`ConveyorSplitter`）を持ち、木の歯車・鉄の歯車・高速は `PreviewOnly` に Cube を載せた仮モデルである。

## 決定と出所

- 合流マスには、そのベルトと同じファミリーの分岐器ブロックのモデルを出す。電気は `ConveyorSplitter`、歯車系と高速は分岐器が仮モデルなので当面は箱になる。
  出所: ユーザー裁定 2026-10-17 原文「合流コンベアだけどモデルをかえたい。今分岐コンベアが真四角のboxがのっかってるモデルだと思うけどこれを分岐コンベアのやつにしたい」→ 選択「ファミリーごとの分岐器モデルを使う」
- 出し方は丸ごと差し替え。合流マスになった直線ベルコンの `BlockGameObject` の描画（Renderer）を無効にし、分岐器モデルを同じ位置・向きで出す。合流が解消されたら覆いを消して描画を戻す。
  出所: ユーザー裁定 2026-10-17 選択「ベルトの描画を丸ごと差し替える」
- マスタのスキーマは変えない。クライアントは「マスタの出力コネクタが複数あるベルト（サーバーの `BeltTopologyCell.IsSplitter` と同じ判定）のうち、blockType と beltSpeedPerSecond が合流マスのベルトと同じもの」を分岐器として選ぶ。該当が0件または2件以上なら覆いを出さずログに残す（Validator 案は後述の裁定で取り下げ）。
  出所: ユーザー裁定 2026-10-17 選択「スキーマは触らず、クライアントで同じファミリー群の分岐器を探す」「blockType と beltSpeedPerSecond が同じ分岐器」
- 届け先は feature/belt-segment-client-sync（PR #1501）。
  出所: ユーザー裁定 2026-10-17 選択「今のブランチ(PR #1501)に積む」
- 分岐器ブロック自身が合流（複数入力）になるマスは、分岐器が自分のモデルを持つので何もしない。覆いの対象は `Kind == Merge` の segment だけ。
  出所: agent前提（サーバーは分岐器の合流を `BeltSegmentKind.Branch` ＋内部segmentとして扱う。`BeltSegmentLayoutBuilder.cs:66-69`）
- 覆いは直線ベルコンの `BlockGameObject` の子にせず、`BeltTransportItems` と同じように専用の親 GameObject の下に置く。ベルコンが撤去されて `BlockGameObject` が消えたら覆いも消す。
  出所: agent前提（子にすると Renderer 一括無効の対象から覆いを除く細工が要る。`AddressableBeltItemObjectFactory` の親 GameObject 方式に揃える）
- 対応付けは `Game.Block.Interface/Extension/BeltSplitterCounterpart`（`.claude/rules/core-master.md`: ドメイン解釈は Game.Xxx.Interface へ）。サーバーの `BeltTopologyBuilder` も同じ `IsSplitter` を使う。
  出所: agent前提
- 覆いの更新は購読でなく、`BeltItemObjectPresenter` と同じく `TrainUnitClientSimulator.Tick` から毎フレーム明示的に呼び、複製の版（`ReplicaVersion`）が進んだ回だけ合流マス集合を取り直す。
  出所: agent前提（`BeltItemObjectPresenter` の先行パターン。2026-10-14 の裁定「表示物の生成は同期化し、呼び出し順は Update から明示する」に揃える）
- Codex 独立質問の消し込み（SKILL §1.7）は省略する。
  出所: ユーザー方針（メモ「Codexは貴重なので使用禁止」）

## 検証範囲

- テストは書かない。テスト用 mod には同型同速の分岐器を置かず、分岐器が引けないベルトは覆いなし（ログ1回）とする。本番マスタ（alpha mod）だけ対応付けが成立していればよい。
  出所: ユーザー裁定 2026-10-17「この合流マスのモデルさしかえに関してはテストいりません」「テストではモデル差し替えしなくていいです。本番環境だけモデルが同じファミリーの分岐器になっていればいいです」
- 上の裁定により、マスタ読込時の Validator（0件・2件以上で拒否）は置かない。0件・2件以上は実行時に `BeltSplitterCounterpart` が LogError を出して覆いを出さない。
- 実プレイ確認はユーザーが行う。

## 影響

- 仕様たたき台 §1.4「箱で隠す」は「ファミリーの分岐器モデルで隠す」に読み替える。仕様書はユーザー管理 repo（MyBeltConvSegment）なので、更新はユーザーが行う。
- 歯車系・高速の分岐器に本物のモデルが入れば、合流マスの見た目も自動で追従する。
