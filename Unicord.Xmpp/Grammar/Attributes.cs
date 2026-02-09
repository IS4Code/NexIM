using System;

namespace Unicord.Xmpp.Grammar;

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class ComplexTypeAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class NamespaceAttribute(string uri) : Attribute
{
    public string Uri => uri;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class NameAttribute(string localName, string? namespaceUri = null) : Attribute
{
    public string LocalName => localName;
    public string? NamespaceUri => namespaceUri;
}
