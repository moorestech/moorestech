# コードレビュー観点

## 1. ドメイン型があるならプリミティブ型を使わない

**観点:** フィールド・引数の型が、同じ意味を持つ既存のドメイン型と一致しているか？

コードベースに`Torque`, `RPM`, `ElectricPower`等のUnitGenerator型がある場合、新しいフィールドや引数でプリミティブ型（`float`, `double`等）を使っていたら疑う。

**悪い例:**
```csharp
private readonly float _requireTorquePerRpm;
```

**良い例:**
```csharp
private readonly Torque _requireTorquePerRpm;
```

「per RPM」のように単位が厳密には異なるケースでも、型の一貫性とキャストの自然さを優先する。

## 2. 導出値ではなく根本原因でガードする

**観点:** ガード条件は、計算の途中結果ではなく入力値を直接チェックしているか？

導出値でガードすると：
- 読み手が「なぜ0になるのか」を逆算する必要がある
- 本当の問題（ゼロ除算等）との距離が遠くなり、ガードの意図が不明確になる

**悪い例:**
```csharp
// requiredTorque は rpm * torquePerRpm の導出値
var requiredTorque = GetRequiredTorque(rpm, isClockwise);
if (requiredTorque.AsPrimitive() <= 0) { ... }
```

**良い例:**
```csharp
// 根本原因であるrpmを直接チェック
if (rpm.AsPrimitive() <= 0) { ... }
```

入力値で直接チェックすれば、ガードの理由が自明になる。

## 3. 呼び出し元のコードを最小化し、判断はサービス内に集約する

**観点:** 呼び出し元は「1本の呼び出し」で済んでいるか？ 同じ意図の判定・分岐が呼び出し側に散らばっていないか？ 減らせないか徹底的に検証する。

呼び出し元が、サービスの内部状態に依存した複数の判定メソッド（`IsXxxCompatible`, `CanYyy` 等）を順番に呼んでから本命の操作を呼ぶ形は疑う。判定ロジックがサービスの外に漏れており、呼び出し箇所が増えるほど同じ分岐を書き写す羽目になる。判定と操作を1つの `TryXxx(..., out string reason)` 等に集約し、呼び出し元は結果と理由だけ受け取れるようにする。判定用メソッドはできる限り公開しない（テストも集約後のAPI経由で書く）。

**悪い例:**
```csharp
// 呼び出し元がサービスの内部判定を3回呼び、分岐を自前で持っている
if (!_selection.CanCommit()) return;
if (!hovered.IsRemovable(out var reason)) { ShowTooltip(reason); return; }
if (!_selection.IsCategoryCompatible(hovered)) { ShowTooltip("別カテゴリー…"); return; }
_selection.AddTarget(hovered);
```

**良い例:**
```csharp
// 判定と追加をサービス内へ集約。呼び出し元は成否と拒否理由だけ受け取る
if (!_selection.TryAddTarget(hovered, out var denyReason))
    ShowTooltip(denyReason);
```

減らせないと結論づける前に「この分岐は本当に呼び出し元にしか書けないか」を必ず問い直す。
