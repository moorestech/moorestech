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
        foreach (var node in ((YamlMappingNode)document.RootNode).Children) Console.WriteLine($"{node.GetType()} {node}");
        
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
        var node = (YamlMappingNode)yamlDocument.RootNode;
        return ParseYamlMappingNode(node, null, null);
    }
    
    private static IJsonNode ParseYamlNode(YamlNode yamlNode, IJsonNode jsonNode, string? propertyName)
    {
        return yamlNode switch
        {
            YamlMappingNode yamlMappingNode => ParseYamlMappingNode(yamlMappingNode, jsonNode, propertyName),
            YamlScalarNode yamlScalarNode => ParseYamlScalarNode(yamlScalarNode, jsonNode, propertyName),
            YamlSequenceNode yamlSequenceNode => ParseYamlSequenceNode(yamlSequenceNode, jsonNode, propertyName),
            YamlAliasNode => throw new Exception("alias is not supported"),
            _ => throw new ArgumentOutOfRangeException(nameof(yamlNode))
        };
    }
    
    private static JsonObject ParseYamlMappingNode(YamlMappingNode yamlMappingNode, IJsonNode parentJsonNode, string? propertyName)
    {
        var dictionary = new Dictionary<string, IJsonNode>();
        var jsonObject = new JsonObject(dictionary, parentJsonNode, propertyName, Location.Create(yamlMappingNode));
        
        foreach (var map in yamlMappingNode.Children)
        {
            var key = map.Key as YamlScalarNode;
            var childParentName = key!.Value;
            var value = ParseYamlNode(map.Value, jsonObject, childParentName);
            dictionary[childParentName] = value;
        }
        
        return jsonObject;
    }
    
    /// <summary>
    ///     boolean(true) boolean(false) integer float string
    ///     上の順で評価
    /// </summary>
    private static IJsonNode ParseYamlScalarNode(YamlScalarNode yamlScalarNode, IJsonNode parentJsonNode, string? propertyName)
    {
        var location = Location.Create(yamlScalarNode);
        
        // boolean
        if (yamlScalarNode.Value is "true" or "True" or "TRUE") return new JsonBoolean(true, parentJsonNode, propertyName, location);
        if (yamlScalarNode.Value is "false" or "False" or "FALSE") return new JsonBoolean(false, parentJsonNode, propertyName, location);
        
        // integer
        if (long.TryParse(yamlScalarNode.Value, out var intValue)) return new JsonInt(intValue, parentJsonNode, propertyName, location);
        
        // float
        if (double.TryParse(yamlScalarNode.Value, out var floatValue)) return new JsonNumber(floatValue, parentJsonNode, propertyName, location);
        
        // string
        return new JsonString(yamlScalarNode.Value ?? "", parentJsonNode, propertyName, location);
    }
    
    private static JsonArray ParseYamlSequenceNode(YamlSequenceNode yamlSequenceNode, IJsonNode parentJsonNode, string? propertyName)
    {
        var nodes = new IJsonNode[yamlSequenceNode.Children.Count];
        var jsonArray = new JsonArray(nodes, parentJsonNode, propertyName, Location.Create(yamlSequenceNode));
        
        for (var i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var child = yamlSequenceNode.Children[i];
            var childJsonNode = ParseYamlNode(child, jsonArray, null);
            nodes[i] = childJsonNode;
        }
        
        return jsonArray;
    }
}
