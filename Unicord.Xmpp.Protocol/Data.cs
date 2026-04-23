using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType, Namespace(XData)]
public interface IDataHandler : IPayloadHandler
{
    [Name("x")]
    ValueTask<IFormHandler> DataForm([Name("type")] Token<FormType>? type);
}

[SimpleType]
public enum FormType
{
    [Name("form")] Form,
    [Name("result")] Result,
    [Name("cancel")] Cancel,
    [Name("submit")] Submit
}

[ComplexType, Namespace(XData)]
public interface IFormHandler : IFormItemHandler
{
    [Name("title")]
    ValueTask Title(LanguageTaggedString? text);

    [Name("item")]
    ValueTask<IFormItemHandler> Item();
}

[ComplexType, Namespace(XData)]
public interface IFieldValueHandler : IPayloadHandler
{
    [Name("value")]
    ValueTask Value(Token<FieldValue>? value);
}

[ComplexType, Namespace(XData)]
public interface IFieldHandler : IFieldValueHandler
{
    [Name("desc")]
    ValueTask Description(LanguageTaggedString? text);

    [Name("required")]
    ValueTask Required();

    [Name("option")]
    ValueTask<IFieldValueHandler> Option([Name("label")] LanguageTaggedString? label);
}

[ComplexType, Namespace(XData)]
public interface IFormItemHandler : IPayloadHandler
{
    [Name("field")]
    ValueTask<IFieldHandler> Field([Name("var")] Token<FieldVariable>? variable, [Name("type")] Token<FieldType>? type, [Name("label")] LanguageTaggedString? label);
}

[SimpleType]
public enum FieldVariable
{
    [Name("FORM_TYPE")] FormType
}

[SimpleType]
public enum FieldValue
{

}

[SimpleType]
public enum FieldType
{
    [Name("boolean")] Boolean,
    [Name("fixed")] Fixed,
    [Name("hidden")] Hidden,
    [Name("jid-multi")] ResourceList,
    [Name("jid-single")] Resource,
    [Name("list-multi")] OptionList,
    [Name("list-single")] Option,
    [Name("text-multi")] TextList,
    [Name("text-single")] Text,
    [Name("text-private")] PrivateText
}
