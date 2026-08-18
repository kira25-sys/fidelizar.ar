namespace Fidelizar.Client.Api;

/// <summary>Outcome of a call with no response body — check Success instead of catching.</summary>
public sealed class ApiResult
{
    private ApiResult(bool success, ApiProblem? problem)
    {
        Success = success;
        Problem = problem;
    }

    public bool Success { get; }
    public ApiProblem? Problem { get; }

    public static ApiResult Ok() => new(true, null);
    public static ApiResult Fail(ApiProblem problem) => new(false, problem);
}

/// <summary>Same as <see cref="ApiResult"/>, carrying the response body on success.</summary>
public sealed class ApiResult<T>
{
    private readonly T? _value;

    private ApiResult(bool success, T? value, ApiProblem? problem)
    {
        Success = success;
        _value = value;
        Problem = problem;
    }

    public bool Success { get; }
    public ApiProblem? Problem { get; }

    /// <summary>Throws if the call failed — check Success first.</summary>
    public T Value => Success
        ? _value!
        : throw new InvalidOperationException("No hay valor: la llamada falló, revisá Problem antes de leer Value.");

    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(ApiProblem problem) => new(false, default, problem);
}
