using System;
using System.Collections.Generic;
using System.IO;
using mooresmaster.Generator.Json;
using YamlDotNet.RepresentationModel;

namespace mooresmaster.Generator.Yaml;

public static class YamlParser
{
    public static JsonObject Parse(string yamlText)
    {
        var document = ParseYamlDocument(yamlText);
        foreach (var node in document.RootNode.AllNodes) Console.WriteLine($"{node.GetType()} {node}");
        
        return ParseYamlNode(document);
    }
    
    private static YamlDocument ParseYamlDocument(string yamlText)
    {
        var input = new StringReader(yamlText);
        var yamlStream = new YamlStream();
        yamlStream.Load(input);
        var document = yamlStream.Documents[0];
        
        return document;
    }
    
    private static JsonObject ParseYamlNode(YamlDocument yamlDocument)
    {
        return null;
    }
    
    private static IJsonNode ParseYamlNode(YamlNode yamlNode, IJsonNode jsonNode, string propertyName)
    {
        return yamlNode switch
        {
            YamlMappingNode yamlMappingNode => ParseYamlMappingNode(yamlMappingNode, jsonNode, propertyName),
            YamlScalarNode yamlScalarNode => throw new NotImplementedException(),
            YamlSequenceNode yamlSequenceNode => throw new NotImplementedException(),
            YamlAliasNode => throw new Exception("alias is not supported"),
            _ => throw new ArgumentOutOfRangeException(nameof(yamlNode))
        };
    }
    
    private static JsonObject ParseYamlMappingNode(YamlMappingNode yamlMappingNode, IJsonNode jsonNode, string propertyName)
    {
        var dictionary = new Dictionary<string, IJsonNode>();
        var parent = new JsonObject(dictionary, jsonNode, propertyName);
        
        foreach (var map in yamlMappingNode.Children)
        {
            var key = map.Key as YamlScalarNode;
            var childParentName = key!.Value;
            var value = ParseYamlNode(map.Value, parent, childParentName);
            dictionary[childParentName] = value;
        }
        
        return parent;
    }
}
