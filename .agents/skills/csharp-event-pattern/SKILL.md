---
name: csharp-event-pattern
description: |
  C#イベント実装パターンガイド。このプロジェクトではC#標準のAction/eventではなくUniRxを使用する。
  Use when:
  1. 新しいイベント/通知システムを実装する時
  2. Subject/Observable/Subscribeパターンを使う時
  3. イベントの購読・破棄パターンを実装する時
  4. 既存イベントのリファクタリングを行う時
---

# C# Event Implementation Pattern Guide

## 基本ルール

**このプロジェクトではC#標準の`event Action`や`event EventHandler`ではなく、UniRxの`Subject<T>` / `IObservable<T>`を使用する。**

## 基本パターン

```csharp
using UniRx;

public class MyEventSource
{
    private readonly Subject<Unit> _onCompleted = new();
    public IObservable<Unit> OnCompleted => _onCompleted;

    public void DoSomething()
    {
        _onCompleted.OnNext(Unit.Default);
    }
}
```

- Subjectはprivateで保持し、`IObservable<T>`としてpublicに公開する
- 値を伴うイベントは`Subject<T>`の型引数で表現する

## 購読の破棄ルール

| コンテキスト | 破棄方法 |
|------------|---------|
| MonoBehaviour | `.AddTo(this)` |
| IInitializable + IDisposable | `CompositeDisposable` + `.AddTo(_subscriptions)` |
| 静的クラス / コンストラクタ購読 | 破棄不要（ライフサイクルと同一） |

## やってはいけないこと

1. **C#標準の`event`を使う** → UniRxの`Subject<T>`を使う
2. **Subjectをpublicにする** → 必ず`IObservable<T>`として公開する
3. **`.Subscribe()`の戻り値を捨てる**（MonoBehaviour内） → `.AddTo(this)`で管理する
4. **イベントハンドラ内でtry-catchする** → 条件分岐で対応する（プロジェクトルール）
