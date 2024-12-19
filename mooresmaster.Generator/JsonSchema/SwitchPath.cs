namespace mooresmaster.Generator.JsonSchema;

public static class SwitchPathParser
{
    public static SwitchPath Parse(string path)
    {
        return new SwitchPath([], SwitchPathType.Absolute);
    }
}

public record SwitchPath(ISwitchPathElement[] Elements, SwitchPathType Type)
{
    public readonly SwitchPathType Type = Type;
    public ISwitchPathElement[] Elements = Elements;
}

public interface ISwitchPathElement;

public enum SwitchPathType
{
    Absolute,
    Relative
}

public record NormalSwitchPathElement(string Path) : ISwitchPathElement
{
    public string Path = Path;
}

public record ParentSwitchPathElement : ISwitchPathElement;
