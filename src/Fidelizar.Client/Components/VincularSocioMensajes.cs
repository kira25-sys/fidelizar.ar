using System.Net;
using Fidelizar.Client.Api;

namespace Fidelizar.Client.Components;

/// <summary>What the dialog shows after a rejected vinculación, and what it offers next to it.</summary>
/// <param name="Mensaje">The Spanish text, already naming the member and the code that was typed.</param>
/// <param name="Tono">"danger" or "warning" — the banner modifier, never colour alone (DESIGN-SYSTEM §11).</param>
/// <param name="OfreceIngresar">A dead session: the banner offers S1 instead of a pointless retry.</param>
/// <param name="ListaDesactualizada">The list on screen no longer matches the server: offer to reload it.</param>
public sealed record VincularSocioRechazo(
    string Mensaje, string Tono, bool OfreceIngresar = false, bool ListaDesactualizada = false);

/// <summary>
/// F1-14 — the server's four rejections (REST-CONTRACT-F1 §"F1-14 Socios sin vincular") turned
/// into Spanish. Pure and separate from the dialog so the wording is testable without a browser.
/// </summary>
public static class VincularSocioMensajes
{
    public static VincularSocioRechazo Rechazo(ApiProblem problem, string nombre, string codigo) => problem switch
    {
        // The one that matters most: the code belongs to another socio, and the person has to
        // understand *that*, not read "error 409".
        { ErrorCode: "CLIENTE_EXTERNO_ID_DUPLICADO" } => new(
            $"El código «{codigo}» ya está asignado a otro socio de este negocio. Cada socio tiene " +
            $"el suyo y no se puede repetir: fijate en el sistema de ventas cuál es el de {nombre}.",
            "danger"),

        { ErrorCode: "MIEMBRO_YA_VINCULADO" } => new(
            $"{nombre} ya quedó vinculado a un código, probablemente desde otra pantalla. " +
            "Actualizá la lista para verla como está ahora.",
            "warning", ListaDesactualizada: true),

        { ErrorCode: "CLIENTE_EXTERNO_ID_REQUERIDO" } => new(
            "Escribí el código de cliente del POS.", "danger"),

        // I8: the server answers the same 404 for "no existe" and "es de otro negocio", so this
        // message must not distinguish them either.
        { StatusCode: HttpStatusCode.NotFound } => new(
            $"No encontramos a {nombre}. La lista puede haber quedado vieja — actualizala y fijate de nuevo.",
            "warning", ListaDesactualizada: true),

        { StatusCode: HttpStatusCode.Unauthorized } => new(
            "Tu sesión venció. Iniciá sesión de nuevo para continuar — no perdés lo que escribiste.",
            "danger", OfreceIngresar: true),

        { StatusCode: HttpStatusCode.Forbidden } => new(
            "No tenés permiso para vincular socios. Esto lo hacen la encargada o el dueño.", "danger"),

        // Offline, timeout, a 500: the code typed stays in the field either way.
        _ => new(
            $"No pudimos vincular a {nombre}. El código que escribiste sigue acá — probá de nuevo.",
            "danger"),
    };
}
