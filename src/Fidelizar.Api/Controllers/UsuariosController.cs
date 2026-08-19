using Fidelizar.Api.Security;
using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Shared.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fidelizar.Api.Controllers;

/// <summary>S10 Usuarios (Dueño only, FUNCTIONAL-SPEC §screen-map) — the staff account CRUD
/// <c>IUsuarioRepository</c> never had, because until now nothing beyond login needed it.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Policies.DuenoOnly)]
public sealed class UsuariosController(IUsuarioService usuarioService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        var usuarios = await usuarioService.ListarAsync(negocioId, cancellationToken);

        var respuesta = usuarios
            .Select(u => new UsuarioResponse(u.Id, u.NombreCompleto, u.Email, u.Rol.ToString(), u.SucursalId, u.Activo))
            .ToList();

        return Ok(respuesta);
    }

    [HttpPost]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        // Rol travels as text on the wire (Shared cannot reference Domain, ARCHITECTURE §3) —
        // parsed here, in Api, the one layer that sees both. Sistema is a CLR enum member but
        // never a valid account: rejected explicitly, not merely by Enum.TryParse's leniency.
        if (!Enum.TryParse<RolUsuario>(request.Rol, ignoreCase: false, out var rol) || rol == RolUsuario.Sistema)
        {
            throw new ValidationException(
                $"Rol '{request.Rol}' no es válido. Los valores admitidos son " +
                $"{Roles.Cajero}, {Roles.Encargada}, {Roles.Dueno} y {Roles.Soporte}.",
                "ROL_INVALIDO");
        }

        var creado = await usuarioService.CrearAsync(
            new CrearUsuarioSolicitud(negocioId, request.NombreCompleto, request.Email, request.Password, rol, request.SucursalId),
            cancellationToken);

        var respuesta = new UsuarioResponse(
            creado.Id, creado.NombreCompleto, creado.Email, creado.Rol.ToString(), creado.SucursalId, creado.Activo);

        return CreatedAtAction(nameof(Listar), respuesta);
    }
}
