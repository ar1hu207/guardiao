using Guardiao.Domain.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Controllers;

/// <summary>Cadastro de motoboys, contatos de emergência e histórico.</summary>
[ApiController]
[Route("api/motoboys")]
[Produces("application/json")]
public sealed class MotoboysController(IMotoboyService motoboys, IOcorrenciaService ocorrencias) : ControllerBase
{
    private static DadosMotoboy Dados(MotoboyRequest r) => new(r.Nome, r.Telefone, r.Placa, r.DispositivoId);

    /// <summary>Cadastra um motoboy. O dispositivo começa desativado até haver um contato.</summary>
    /// <response code="201">Motoboy cadastrado.</response>
    /// <response code="400">Dado inválido (placa, telefone, nome).</response>
    /// <response code="409">Placa ou dispositivo já usados.</response>
    [HttpPost]
    [ProducesResponseType<MotoboyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MotoboyResponse>> Cadastrar(MotoboyRequest req, CancellationToken ct)
    {
        var m = await motoboys.CadastrarAsync(Dados(req), ct);
        return CreatedAtAction(nameof(Obter), new { id = m.Id }, MotoboyResponse.De(m));
    }

    /// <summary>Lista os motoboys, em ordem alfabética.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<MotoboyResponse>>(StatusCodes.Status200OK)]
    public async Task<IEnumerable<MotoboyResponse>> Listar(CancellationToken ct) =>
        (await motoboys.ListarAsync(ct)).Select(MotoboyResponse.De);

    /// <summary>Consulta um motoboy.</summary>
    /// <response code="404">Motoboy não encontrado.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<MotoboyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<MotoboyResponse> Obter(int id, CancellationToken ct) => MotoboyResponse.De(await motoboys.ObterAsync(id, ct));

    /// <summary>Atualiza nome, telefone e placa. O dispositivoId não muda.</summary>
    /// <response code="400">Dado inválido ou tentativa de trocar o dispositivo.</response>
    /// <response code="404">Motoboy não encontrado.</response>
    /// <response code="409">Placa já usada por outro motoboy.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<MotoboyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<MotoboyResponse> Atualizar(int id, MotoboyRequest req, CancellationToken ct) =>
        MotoboyResponse.De(await motoboys.AtualizarAsync(id, Dados(req), ct));

    /// <summary>Remove o motoboy (e seus contatos e histórico).</summary>
    /// <response code="204">Removido.</response>
    /// <response code="404">Motoboy não encontrado.</response>
    /// <response code="409">Há ocorrência aberta.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        await motoboys.RemoverAsync(id, ct);
        return NoContent();
    }

    /// <summary>Histórico de ocorrências do motoboy, da mais recente para a mais antiga.</summary>
    /// <response code="404">Motoboy não encontrado.</response>
    [HttpGet("{id:int}/ocorrencias")]
    [ProducesResponseType<IEnumerable<OcorrenciaResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IEnumerable<OcorrenciaResponse>> Historico(int id, CancellationToken ct) =>
        (await ocorrencias.HistoricoDoMotoboyAsync(id, ct)).Select(OcorrenciaResponse.De);

    /// <summary>Adiciona um contato de emergência (máximo 5).</summary>
    /// <response code="201">Contato adicionado.</response>
    /// <response code="400">Dado inválido.</response>
    /// <response code="404">Motoboy não encontrado.</response>
    /// <response code="409">Limite atingido ou telefone repetido.</response>
    [HttpPost("{id:int}/contatos")]
    [ProducesResponseType<ContatoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContatoResponse>> AdicionarContato(int id, ContatoRequest req, CancellationToken ct)
    {
        var c = await motoboys.AdicionarContatoAsync(id, new DadosContato(req.Nome, req.Telefone, req.Parentesco), ct);
        return CreatedAtAction(nameof(ListarContatos), new { id }, ContatoResponse.De(c));
    }

    /// <summary>Lista os contatos de emergência do motoboy.</summary>
    /// <response code="404">Motoboy não encontrado.</response>
    [HttpGet("{id:int}/contatos")]
    [ProducesResponseType<IEnumerable<ContatoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IEnumerable<ContatoResponse>> ListarContatos(int id, CancellationToken ct) =>
        (await motoboys.ListarContatosAsync(id, ct)).Select(ContatoResponse.De);

    /// <summary>Remove um contato. Com o dispositivo ativo, o último contato não pode sair.</summary>
    /// <response code="204">Removido.</response>
    /// <response code="404">Motoboy ou contato não encontrado.</response>
    /// <response code="409">Seria o último contato com o dispositivo ativo.</response>
    [HttpDelete("{id:int}/contatos/{contatoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoverContato(int id, int contatoId, CancellationToken ct)
    {
        await motoboys.RemoverContatoAsync(id, contatoId, ct);
        return NoContent();
    }

    /// <summary>Ativa a caixinha da moto. Exige pelo menos um contato de emergência.</summary>
    /// <response code="404">Motoboy não encontrado.</response>
    /// <response code="409">Sem contato de emergência.</response>
    [HttpPatch("{id:int}/dispositivo/ativar")]
    [ProducesResponseType<MotoboyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<MotoboyResponse> AtivarDispositivo(int id, CancellationToken ct) =>
        MotoboyResponse.De(await motoboys.AtivarDispositivoAsync(id, ct));

    /// <summary>Desativa a caixinha da moto (quedas deixam de ser aceitas; o SOS do app continua).</summary>
    /// <response code="404">Motoboy não encontrado.</response>
    [HttpPatch("{id:int}/dispositivo/desativar")]
    [ProducesResponseType<MotoboyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<MotoboyResponse> DesativarDispositivo(int id, CancellationToken ct) =>
        MotoboyResponse.De(await motoboys.DesativarDispositivoAsync(id, ct));
}
