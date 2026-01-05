namespace mooresmaster.Generator.Common;

public readonly struct Falliable<T>
{
    public T? Value { get; }
    public bool IsValid { get; }
    
    public static Falliable<T> Success(T value)
    {
        return new Falliable<T>(value, true);
    }
    
    public static Falliable<T> Failure()
    {
        return new Falliable<T>(default, false);
    }
    
    private Falliable(T? value, bool isValid)
    {
        Value = value;
        IsValid = isValid;
    }
}