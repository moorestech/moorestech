# mapObjectPinの解決規則はMapObjectMasterへ集約し、pinTargetTypeの2種類は維持する

2026-08-23 ユーザー裁定（moores-code-review C9 / D4）。

## 決定
`pinTargetParam` → 候補mapObject集合 の解決規則を `Core.Master.MapObjectMaster.ResolvePinTargets` へ集約し、
client（`MapObjectPin.ApplyTutorial`）と server（`ChallengeMasterUtil.TutorialValidation`）が同一メソッドを呼ぶ。
`pinTargetType` は `mapObject` / `earnItem` の2種類を維持する。

## 棄却案: earnItemへ一本化
一度は「earnItemに一本化」で裁定したが、実データ確認で覆した。
小石ピンを `earnItem: 小石` へ寄せると候補が1件→89件（PickUp 4件 / Mining 85件）に増え、
道具を持たないチャレンジ#1の時点で「採掘が必要な岩」へピンが刺さり得る（最初のチュートリアルの退行）。
`earnItem` 解決に「いま入手可能か」のフィルタを足す案もあったが、ピン解決に採取可否の判断を持ち込むため見送った。

## 帰結
- 第3のpinTargetType追加時の更新点が1箇所になり、「候補0件＝達成不能なピン」の検証が全種別へ自動で効く
- `MapObjectPinTargetResolver` は `Core.Master` 側へ移り、client専用クラスとしては消える
