using System;

namespace Unicord.Primitives.Json.Grammar;

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ComplexTypeAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class SimpleTypeAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class NameAttribute(string localName) : Attribute
{
    public string LocalName => localName;
}

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class KeyAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ValueKindAttribute(ValueKind kind) : Attribute
{
    public ValueKind Kind => kind;
}

public enum ValueKind
{
    Scalar,
    Array,
    Object
}
