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
