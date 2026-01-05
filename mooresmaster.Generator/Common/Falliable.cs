namespace mooresmaster.Generator.Common;

public readonly struct Falliable<T>
{
    public T? Value { get; }
    public bool IsValid { get; }

    public static Falliable<T> Success(T value) => new(value, true);
    public static Falliable<T> Failure() => new(default, false);

    private Falliable(T? value, bool isValid)
    {
        Value = value;
        IsValid = isValid;
    }
}
