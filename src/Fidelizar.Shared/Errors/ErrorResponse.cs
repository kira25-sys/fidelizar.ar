namespace Fidelizar.Shared.Errors;

/// <summary>
/// The wire shape every error response takes, whatever endpoint produced it. Fidelizar.Api's
/// ExceptionHandlingMiddleware is the only thing that constructs one; Fidelizar.Client
/// deserialises the same type, so a failed request never needs ad hoc parsing on either side.
///
/// Ported from Dsw2026Tpi (ARCHITECTURE §15) and moved here, out of a CrossCutting project that
/// this product does not have — Shared holds no logic, only the DTO shape.
/// </summary>
public sealed record ErrorResponse(string ErrorCode, string Message)
{
    public ICollection<ErrorDetail> Details { get; init; } = [];

    public void AddDetail(string field, string issue)
    {
        Details.Add(new ErrorDetail(field, issue));
    }

    public void AddDetails(IEnumerable<(string Field, string Issue)> details)
    {
        foreach (var (field, issue) in details)
        {
            AddDetail(field, issue);
        }
    }
}

/// <summary>A single field-level complaint carried by an <see cref="ErrorResponse"/>.</summary>
public sealed record ErrorDetail(string Field, string Issue);
