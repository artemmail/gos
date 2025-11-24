using System;

namespace Zakupki.Fetcher.Models;

public sealed class QueryVectorPendingException : Exception
{
    public QueryVectorPendingException(Guid requestId)
        : base("Vector generation is still in progress")
    {
        RequestId = requestId;
    }

    public Guid RequestId { get; }
}
