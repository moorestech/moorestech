# Web UI E2E Coverage Design

## Scope

TODO.md の実装済み A2-A5、B1-B3、C1-C3、C4先行分、D-i18n を既存 Playwright spec と照合し、未担保の wire topic/action から UI までを追加する。skit、tutorial、tutorialAnchor の新規 spec は対象外とする。

## Architecture

mock host に型付きシナリオ別 HTTP control を追加する。各 control は topic と同じ payload 型の可変 snapshot を更新し、現在の購読者へ event を配信する。全 topic の購読者を共通レジストリでも追跡し、再接続時は可変状態を snapshot として返す。

revision の逆転試験だけは control から明示 revision を指定できる。再接続後の新 snapshot を受理した後、旧世代相当の低い revision を持つ snapshot を送り、表示が戻らないことを確認する。

## Test Boundaries

- connection: disconnect、restoring、全 snapshot 復元、open 復帰、旧 revision 破棄
- parity details: split drag の配分結果、機械分間生産数、研究報酬数
- ui state: 許可外 action の `transition_not_allowed` と画面維持
- common HUD: placement、delete、mining、key hints、crosshair、visibility、tooltip、context menu action
- pause/train/cutscene: state routing、action、error/disconnect、復帰
- i18n/challenge: topic event、辞書再取得または HUD 更新

## Determinism

固定 sleep は使わず、locator assertion と `expect.poll` を使う。control は response 前に state を更新して event を送る。spec 後処理で可変状態を既定値へ戻す。

## Counterexample

再接続後に新しい snapshot が描画された状態へ、低い revision の旧 snapshot を注入する。revision gate が壊れていれば表示が旧値へ戻るため、UI の不変 assertion で検出する。
