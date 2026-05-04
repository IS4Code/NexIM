using System.Net.Mail;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType, Namespace(IqAuth)]
public interface IAuthQueryHandler : IPayloadHandler
{
    [Name("username")]
    ValueTask Username(string? value);

    [Name("password")]
    ValueTask Password(TemporaryString? value);

    [Name("digest")]
    ValueTask Digest(string? value);

    [Name("resource")]
    ValueTask Resource(string? value);
}

[ComplexType, Namespace(IqRegister)]
public interface IRegisterQueryHandler : IPayloadHandler, IDataHandler, IExternalDataHandler
{
    [Name("registered")] ValueTask Registered();
    [Name("remove")] ValueTask Remove();

    [Name("instructions")] ValueTask Instructions(LanguageTaggedString? text);
    [Name("username")] ValueTask Username(string? value);
    [Name("nick")] ValueTask Nickname(string? value);
    [Name("password")] ValueTask Password(TemporaryString? value);
    [Name("name")] ValueTask Name(string? value);
    [Name("first")] ValueTask FirstName(string? value);
    [Name("last")] ValueTask LastName(string? value);
    [Name("email")] ValueTask EmailAddress(MailAddress? value);
    [Name("address")] ValueTask Address(string? value);
    [Name("city")] ValueTask City(string? value);
    [Name("state")] ValueTask State(string? value);
    [Name("zip")] ValueTask ZipCode(string? value);
    [Name("phone")] ValueTask PhoneNumber(string? value);
    [Name("url")] ValueTask Url(ValueUri? value);
    [Name("date")] ValueTask Date(DateComponents? value);

    // Obsolete
    [Name("misc")] ValueTask Miscellaneous(string? value);
    [Name("text")] ValueTask Text(string? value);
    [Name("key")] ValueTask Key(string? value);
}
