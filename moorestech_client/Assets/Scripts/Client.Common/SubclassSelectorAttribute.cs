// Based on https://github.com/baba-s/Unity-SerializeReferenceExtensions

using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SubclassSelectorAttribute : PropertyAttribute
{
    bool m_includeMono;

    public SubclassSelectorAttribute(bool includeMono = false)
    {
        m_includeMono = includeMono;
    }

    public bool IsIncludeMono()
    {
        return m_includeMono;
    }
}
