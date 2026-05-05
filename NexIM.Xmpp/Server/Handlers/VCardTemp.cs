using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Formats;

namespace NexIM.Xmpp.Server.Handlers;

internal sealed class GetVCardTemp : BaseDelegatingVCardHandler<CapturingHandler<IVCardHandler>, EmptyDisposable, ICommandContext>
{
    protected sealed override CapturingHandler<IVCardHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    private VCardQueryData GetData()
    {
        return new VCardQueryData {
            VCard = null,
            Extensions = InnerHandler.ToExtensions()
        };
    }

    private RetrieveEvent GetEvent()
    {
        return new RetrieveEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal abstract class DataVCardTemp : BaseDelegatingVCardHandler<VCardParser<ICommandContext>, EmptyDisposable, ICommandContext>, IDisposable
{
    protected sealed override VCardParser<ICommandContext> InnerHandler { get; } = new(new());
    protected sealed override EmptyDisposable Disposable => default;

    protected VCardQueryData GetData()
    {
        var vcard = InnerHandler.VCard;
        foreach(var data in vcard.Data)
        {
            // Each embedded file must change ownership to the account
            if(data.BinaryValue?.TryGetValue(out var file) == true && file is not UploadedFile)
            {
                string? contentType = data switch {
                    VCardMedia media => media.FormatType,
                    _ => null
                };
                data.BinaryValue = new(this.GetClientSession().AcquireUploadedFile(file, null, contentType));
                file.Dispose();
            }
        }
        return new VCardQueryData {
            VCard = vcard,
            Extensions = InnerHandler.ExtensionsHandler.ToExtensions()
        };
    }

    protected abstract QueryEvent GetEvent();

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }

    public void Dispose()
    {
        using var data = InnerHandler.VCard.Data.GetEnumerator();
        DisposeRest();

        bool DisposeRest()
        {
            while(data.MoveNext())
            {
                try
                {
                    // Already uploaded files will be unaffected by dispose
                    if(data.Current.BinaryValue?.TryGetValue(out var file) == true)
                    {
                        file.Dispose();
                    }
                }
                catch when(DisposeRest())
                {
                    throw;
                }
            }
            return false;
        }
    }
}

internal sealed class SetVCardTemp : DataVCardTemp
{
    protected override QueryEvent GetEvent()
    {
        return new UpdateEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}

internal sealed class ResultVCardTemp : DataVCardTemp
{
    protected override QueryEvent GetEvent()
    {
        return new ResponseEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}
