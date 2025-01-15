namespace mooresmaster.Generator;

public static class Tokens
{
    public const string IsSourceGeneratorDebug = "IsSourceGeneratorDebug";
    public const string LoaderFileName = "mooresmaster.loader.g.cs";
    public const string BuiltinLoaderFileName = "mooresmaster.loader.BuiltinLoader.g.cs";
    public const string ExceptionFileName = "mooresmaster.loader.exception.g.cs";
    public const string ErrorFileName = "mooresmaster.error.g.cs";
    
    public const string DefineInterface = "defineInterface";
    public const string GlobalDefineInterface = "globalDefineInterface";
    public const string InterfaceNameKey = "interfaceName";
    public const string PropertiesKey = "properties";
    public const string PropertyNameKey = "key";
    public const string ImplementationInterfaceKey = "implementationInterface";
    public const string SwitchKey = "switch";
    public const string RefKey = "ref";
    public const string TypeKey = "type";
    
    public const string ObjectType = "object";
    public const string ArrayType = "array";
    public const string StringType = "string";
    public const string NumberType = "number";
}
