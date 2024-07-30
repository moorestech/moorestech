namespace mooresmaster.Generator.Util;

public static class StringExtension
{
    public static string ToCamelCase(this string name)
    {
        return name.Substring(0, 1).ToUpper() + name.Substring(1);
    }
}