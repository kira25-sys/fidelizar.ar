namespace Fidelizar.Api.Auth;

/// <summary>
/// Login's request body. Deliberately not in <c>Fidelizar.Shared</c>: F1-04b owns the phase-1
/// REST contract and its DTOs; this is the minimal shape F1-03 needs to prove the session
/// endpoints work, kept local to <c>Fidelizar.Api</c> so it does not preempt that design.
/// </summary>
public sealed record LoginRequest(string Email, string Password);
