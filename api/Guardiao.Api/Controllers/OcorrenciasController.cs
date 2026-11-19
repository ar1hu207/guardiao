using Guardiao.Domain.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Controllers;

/// <summary>Pedidos de socorro: quedas do ESP32 e SOS do app.</summary>
[ApiController]
[Route("api/ocorrencias")]
[Produces("application/json")]
public sealed class OcorrenciasController(IOcorrenciaService servico, TimeProvider relogio) : ControllerBase
{
    /// <summary>Registra uma queda ou SOS e avisa os contatos de emergência.</summary>
    /// <remarks>
    /// Chamado pelo ESP32 quando detecta a queda, ou pelo app no botão de SOS. Acha o motoboy pelo
    /// <c>dispositivoId</c>, salva a ocorrência, consulta o risco do local e avisa cada contato.
    /// </remarks>
    /// <response code="201">Ocorrência registrada e contatos avisados.</response>
    /// <response code="400">Dado inválido (ex.: QUEDA sem intensidadeG, coordenada fora do globo).</response>
    /// <response code="404">Dispositivo não cadastrado.</response>
    /// <response code="409">Dispositivo desativado.</response>
    [HttpPost]
    [ProducesResponseType<OcorrenciaRegistradaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OcorrenciaRegistradaResponse>> Registrar(OcorrenciaRequest req, CancellationToken ct)
    {
        var resultado = await servico.RegistrarAsync(new NovaOcorrencia(
            req.DispositivoId, req.Tipo!.Value, req.Latitude!.Value, req.Longitude!.Value, req.IntensidadeG,
            req.DataHora ?? relogio.GetUtcNow()), ct);
        var resposta = OcorrenciaRegistradaResponse.De(resultado);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Ocorrencia.Id }, resposta);
    }

    /// <summary>Consulta uma ocorrência.</summary>
    /// <response code="200">Ocorrência encontrada.</response>
    /// <response code="404">Ocorrência não encontrada.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OcorrenciaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<OcorrenciaResponse> Obter(int id, CancellationToken ct) =>
        OcorrenciaResponse.De(await servico.ObterAsync(id, ct));

    /// <summary>Cancela um alarme falso. Só ocorrências ABERTAS podem ser canceladas.</summary>
    /// <response code="200">Ocorrência cancelada.</response>
    /// <response code="404">Ocorrência não encontrada.</response>
    /// <response code="409">A ocorrência não está aberta.</response>
    [HttpPatch("{id:int}/cancelar")]
    [ProducesResponseType<OcorrenciaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<OcorrenciaResponse> Cancelar(int id, CancellationToken ct) =>
        OcorrenciaResponse.De(await servico.CancelarAsync(id, ct));

    /// <summary>Marca que o socorro chegou. Só ocorrências ABERTAS.</summary>
    /// <response code="200">Ocorrência atendida.</response>
    /// <response code="404">Ocorrência não encontrada.</response>
    /// <response code="409">A ocorrência não está aberta.</response>
    [HttpPatch("{id:int}/atender")]
    [ProducesResponseType<OcorrenciaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<OcorrenciaResponse> Atender(int id, CancellationToken ct) =>
        OcorrenciaResponse.De(await servico.MarcarAtendidaAsync(id, ct));

    /// <summary>Contagem por tipo e status e o indicador de impacto (tempo médio até o aviso).</summary>
    [HttpGet("estatisticas")]
    [ProducesResponseType<EstatisticasResponse>(StatusCodes.Status200OK)]
    public async Task<EstatisticasResponse> Estatisticas(CancellationToken ct) =>
        EstatisticasResponse.De(await servico.EstatisticasAsync(ct));
}
