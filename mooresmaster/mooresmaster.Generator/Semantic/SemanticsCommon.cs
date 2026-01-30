using System.Collections.Generic;
using System.Linq;

namespace mooresmaster.Generator.Semantic;

public static class SemanticsCommon
{
    public static InterfaceId[] GetAllImplementations(this Semantics semantics, InterfaceId target)
    {
        var checkedInterfaces = new HashSet<InterfaceId>();
        var uncheckedInterfaces = new Stack<InterfaceId>();
        
        uncheckedInterfaces.Push(target);
        
        while (uncheckedInterfaces.Count > 0)
        {
            var interfaceId = uncheckedInterfaces.Pop();
            if (!checkedInterfaces.Add(interfaceId)) continue;
            
            foreach (var implementation in semantics.GetImplementations(interfaceId)) uncheckedInterfaces.Push(implementation);
        }
        
        return checkedInterfaces.ToArray();
    }
    
    public static InterfaceId[] GetImplementations(this Semantics semantics, InterfaceId target)
    {
        var list = new List<InterfaceId>();
        
        if (semantics.InterfaceInterfaceImplementationTable.TryGetValue(target, out var list2)) list = list2;
        
        return list.ToArray();
    }
}