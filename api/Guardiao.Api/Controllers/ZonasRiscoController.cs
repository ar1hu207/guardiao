using System.ComponentModel.DataAnnotations;
using Guardiao.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Controllers;

/// <summary>Risco da região naquele horário, calculado pelo modelo de ML (Infosiga SP).</summary>
[ApiController]
[Route("api/zonas-risco")]
[Produces("application/json")]
public sealed class ZonasRiscoController(IZonaRiscoProvider provedor) : ControllerBase
{
    /// <summary>Zonas de risco perto do motoboy na faixa horária pedida.</summary>
    /// <remarks>
    /// Devolve as zonas num raio ao redor do ponto, da mais próxima para a mais distante.
    /// A primeira é a zona onde o motoboy está. Faixas: madrugada 0–5h, manhã 6–11h, tarde 12–17h, noite 18–23h.
    /// A resposta indica a origem no cabeçalho <c>X-Origem-Risco</c> (banco local ou Serviço de Risco).
    /// </remarks>
    /// <param name="lat" example="-23.5905">Latitude do motoboy.</param>
    /// <param name="lng" example="-46.7889">Longitude do motoboy.</param>
    /// <param name="hora" example="2">Hora do dia (0 a 23).</param>
    /// <param name="raioKm" example="2">Raio da busca em km (padrão 2).</param>
    /// <param name="limite" example="5">Máximo de zonas (padrão 5).</param>
    /// <response code="200">Lista de zonas (vazia se não houver zona no raio).</response>
    /// <response code="400">Parâmetro inválido (ex.: hora 25).</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<ZonaRiscoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<ZonaRiscoResponse>> Consultar(
        [FromQuery, Required, Range(-90, 90)] double? lat,
        [FromQuery, Required, Range(-180, 180)] double? lng,
        [FromQuery, Required] int? hora,
        [FromQuery, Range(0.1, 20)] double raioKm = 2,
        [FromQuery, Range(1, 50)] int limite = 5,
        CancellationToken ct = default)
    {
        Response.Headers["X-Origem-Risco"] = provedor.Origem;
        var zonas = await provedor.ConsultarAsync(lat!.Value, lng!.Value, hora!.Value, raioKm, limite, ct);
        return zonas.Select(ZonaRiscoResponse.De);
    }
}
