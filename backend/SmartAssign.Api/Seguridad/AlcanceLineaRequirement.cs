using Microsoft.AspNetCore.Authorization;

namespace SmartAssign.Api.Seguridad;

/// <summary>
/// Requisito de recurso: el recurso es la línea (byte) que el endpoint
/// intenta exponer. AUTORIZADO = (el rol permite la operación) Y (el
/// alcance cubre la línea) — 04 §6.2.
/// </summary>
public class AlcanceLineaRequirement : IAuthorizationRequirement;
