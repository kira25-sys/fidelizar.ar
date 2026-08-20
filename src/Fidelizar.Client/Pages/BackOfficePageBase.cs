using Fidelizar.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace Fidelizar.Client.Pages;

/// <summary>
/// Session and role plumbing shared by the two back-office member screens, S6 Ficha completa and
/// S7 Historial de movimientos (FUNCTIONAL-SPEC §3: Encargada and Dueño, never Cajero).
///
/// <para><b><see cref="Autorizado"/> is presentation, never protection.</b> It exists so a
/// <c>Cajero</c> who types the URL reads a Spanish explanation instead of watching a request
/// fail, and so the screen does not fire a call it already knows will be refused. Both endpoints
/// carry <c>[Authorize(Policy = EncargadaOrAbove)]</c> server-side and answer <c>403</c>
/// regardless of what this property returns — which is why both pages still handle that
/// <c>403</c> on the way back.</para>
/// </summary>
public abstract class BackOfficePageBase : ComponentBase, IDisposable
{
    [Inject] protected ISessionService SessionService { get; set; } = default!;

    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected bool Autorizado => Roles.EsEncargadaODueno(SessionService.Current?.Rol);

    protected override void OnInitialized()
    {
        SessionService.Changed += OnSessionChanged;
        RedirectIfAnonymous();
    }

    private void OnSessionChanged()
    {
        RedirectIfAnonymous();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>A session that expired mid-read lands back on S1, never on a half-rendered screen.</summary>
    private void RedirectIfAnonymous()
    {
        if (!SessionService.IsAuthenticated)
        {
            Navigation.NavigateTo("/ingreso", replace: true);
        }
    }

    /// <summary>Pages that own a <c>CancellationTokenSource</c> override this and call back into it.</summary>
    public virtual void Dispose() => SessionService.Changed -= OnSessionChanged;
}
