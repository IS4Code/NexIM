global using StatusReports = Unicord.Server.Tools.NonEmptySet<Unicord.Server.Events.StatusReport>;
using System;
using System.Runtime.InteropServices;
using Unicord.Server.Accounts;

namespace Unicord.Server.Events;

public enum StatusCode
{
    #region Success or preliminary success

    /// <summary>
    /// Indicates the operation to process the event has started,
    /// and any further responses will be returned asynchronously.
    /// </summary>
    Received = 0,

    /// <summary>
    /// Indicates the operation was fully processed.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, an <c>&lt;iq&gt;</c> stanza should be automatically confirmed.</item>
    /// </list>
    /// </remarks>
    Success = 10,

    #endregion

    #region Destination errors

    /// <summary>
    /// Indicates the event's target item or recipient is not available.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;service-unavailable&gt;</c>.</item>
    /// </list>
    /// </remarks>
    Unavailable = 100,

    /// <summary>
    /// Indicates the event's target does not exist.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;item-not-found&gt;</c>.</item>
    /// </list>
    /// </remarks>
    NotFound = 110,

    /// <summary>
    /// Indicates the event's target's server does not exist.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;remote-server-not-found&gt;</c>.</item>
    /// </list>
    /// </remarks>
    ServerNotFound = 120,

    /// <summary>
    /// Indicates the event could not be delivered to its recipient.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;recipient-unavailable&gt;</c>.</item>
    /// </list>
    /// </remarks>
    RecipientUnreachable = 130,

    /// <summary>
    /// Indicates the event could not be delivered to its recipient's server.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;remote-server-timeout&gt;</c>.</item>
    /// </list>
    /// </remarks>
    ServerUnreachable = 140,

    /// <summary>
    /// Indicates that the event's recipient is no longer located at the address.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;gone&gt;</c>.</item>
    /// </list>
    /// </remarks>
    RecipientGone = 150,

    /// <summary>
    /// Indicates that the event's intended recipient is located elsewhere.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;redirect&gt;</c>.</item>
    /// </list>
    /// </remarks>
    RecipientChanged = 160,

    #endregion

    #region Operation errors

    /// <summary>
    /// Indicates the sender is not permitted to the action.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;forbidden&gt;</c>.</item>
    /// </list>
    /// </remarks>
    Unauthorized = 200,

    /// <summary>
    /// Indicates the sender must first be registered by the target.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;registration-required&gt;</c>.</item>
    /// </list>
    /// </remarks>
    RegistrationRequired = 210,

    /// <summary>
    /// Indicates the sender must first be authenticated by the target.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;not-authorized&gt;</c>.</item>
    /// </list>
    /// </remarks>
    AuthenticationRequired = 220,

    /// <summary>
    /// Indicates the sender must first be subscribed to the target.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;subscription-required&gt;</c>.</item>
    /// </list>
    /// </remarks>
    SubscriptionRequired = 230,

    /// <summary>
    /// Indicates the action is not permitted because it creates an item that already exists.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;conflict&gt;</c>.</item>
    /// </list>
    /// </remarks>
    AlreadyExists = 240,

    /// <summary>
    /// Indicates the action is not permitted at all by the target.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;not-allowed&gt;</c>.</item>
    /// </list>
    /// </remarks>
    Prohibited = 250,

    #endregion

    #region Request errors

    /// <summary>
    /// Indicates the request is not valid according to the schema.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;bad-request&gt;</c>.</item>
    /// </list>
    /// </remarks>
    InvalidRequest = 300,

    /// <summary>
    /// Indicates the request is not recognized and cannot be performed.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;feature-not-implemented&gt;</c> with <c>type="cancel"</c>.</item>
    /// </list>
    /// </remarks>
    UnrecognizedRequest = 310,

    /// <summary>
    /// Indicates the request is valid but not expected based on the state of the target.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;unexpected-request&gt;</c> with <c>type="modify"</c>.</item>
    /// </list>
    /// </remarks>
    UnexpectedRequest = 320,

    /// <summary>
    /// Indicates the request contains a parameter that is not within the range of valid values.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;not-acceptable&gt;</c>.</item>
    /// </list>
    /// </remarks>
    InvalidParameter = 330,

    /// <summary>
    /// Indicates the request contains a parameter that is not recognized.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;feature-not-implemented&gt;</c> with <c>type="modify"</c>.</item>
    /// </list>
    /// </remarks>
    UnrecognizedParameter = 340,

    /// <summary>
    /// Indicates the request contains an address that is not syntactically valid.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;jid-malformed&gt;</c>.</item>
    /// </list>
    /// </remarks>
    InvalidAddress = 350,

    #endregion

    #region Transport errors

    /// <summary>
    /// Indicates the request cannot be handled due to an unexpected error.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;internal-server-error&gt;</c>.</item>
    /// </list>
    /// </remarks>
    InternalError = 400,

    /// <summary>
    /// Indicates the request will not be processed due to its content.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;policy-violation&gt;</c> with <c>type="modify"</c>.</item>
    /// </list>
    /// </remarks>
    Blocked = 410,

    /// <summary>
    /// Indicates the request will not be processed at this time.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;policy-violation&gt;</c> with <c>type="wait"</c>.</item>
    /// </list>
    /// </remarks>
    ImproperTime = 420,

    /// <summary>
    /// Indicates the recipient is not yet ready to process the request.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;unexpected-request&gt;</c> with <c>type="wait"</c>.</item>
    /// </list>
    /// </remarks>
    NotReady = 430,

    /// <summary>
    /// Indicates there are not enough resources available to process the request.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;resource-constraint&gt;</c>.</item>
    /// </list>
    /// </remarks>
    InsufficientResources = 440,

    /// <summary>
    /// Indicates an error with no recognized condition.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>In XMPP, this corresponds to <c>&lt;undefined-condition&gt;</c>.</item>
    /// </list>
    /// </remarks>
    UnknownError = 450,

    #endregion
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct StatusReport(Identifier Source, StatusCode Code) : IComparable<StatusReport>
{
    public int CompareTo(StatusReport other)
    {
        int cmp = Source.CompareTo(other.Source);
        if(cmp != 0)
        {
            return cmp;
        }
        return ((int)Code).CompareTo((int)other.Code);
    }
}
