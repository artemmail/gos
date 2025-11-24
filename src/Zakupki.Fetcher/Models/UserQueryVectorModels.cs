using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Models;

public sealed class CreateUserQueryVectorRequest
{
    public string? Query { get; set; }
}

public sealed class QueryVectorRequestItem
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string String { get; init; } = string.Empty;
}

public sealed class QueryVectorBatchRequest
{
    public string ServiceId { get; init; } = "AddUserSemanticReq";

    public IReadOnlyList<QueryVectorRequestItem> Items { get; init; } = Array.Empty<QueryVectorRequestItem>();
}

public sealed class UserQueryVectorDto
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Query { get; init; } = string.Empty;

    public IReadOnlyList<float>? Vector { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}

public sealed class QueryVectorResult
{
    public Guid Id { get; init; }

    public string? UserId { get; init; }

    public string? Query { get; init; }

    public IReadOnlyList<float>? Vector { get; init; }
}

public sealed class QueryVectorResponseItem
{
    public Guid Id { get; init; }

    public string? UserId { get; init; }

    public string? String { get; init; }

    public IReadOnlyList<float>? Vector { get; init; }
}

public sealed class QueryVectorBatchResponse
{
    public string? ServiceId { get; init; }

    public IReadOnlyList<QueryVectorResponseItem>? Items { get; init; }
}
