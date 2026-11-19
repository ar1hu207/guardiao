using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;

namespace Guardiao.Domain.Servicos;

public sealed record NovaOcorrencia(string DispositivoId, TipoOcorrencia Tipo, double Latitude, double Longitude,
                                    double? IntensidadeG, DateTimeOffset DataHora);

public sealed record OcorrenciaRegistrada(Ocorrencia Ocorrencia, Motoboy Motoboy, ZonaConsultada? ZonaNoLocal,
                                          IReadOnlyList<ContatoEmergencia> ContatosAvisados);

public sealed record EstatisticasOcorrencias(int Total, IReadOnlyDictionary<TipoOcorrencia, int> PorTipo,
                                             IReadOnlyDictionary<StatusOcorrencia, int> PorStatus,
                                             double? TempoMedioAteAvisoSegundos, double? PercentualAvisoEmAte30s);

public interface IOcorrenciaService
{
    Task<OcorrenciaRegistrada> RegistrarAsync(NovaOcorrencia nova, CancellationToken ct = default);
    Task<Ocorrencia> ObterAsync(int id, CancellationToken ct = default);
    Task<Ocorrencia> CancelarAsync(int id, CancellationToken ct = default);
    Task<Ocorrencia> MarcarAtendidaAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Ocorrencia>> HistoricoDoMotoboyAsync(int motoboyId, CancellationToken ct = default);
    Task<EstatisticasOcorrencias> EstatisticasAsync(CancellationToken ct = default);
}

public sealed class OcorrenciaService(
    IMotoboyRepository motoboys,
    IOcorrenciaRepository ocorrencias,
    IZonaRiscoProvider zonas,
    IEnumerable<INotificador> notificadores,
    TimeProvider relogio) : IOcorrenciaService
{
    /// <summary>
    /// Fluxo principal do Guardião: acha o motoboy pelo dispositivo, salva a ocorrência,
    /// descobre o risco do local e avisa cada contato de emergência em todos os canais.
    /// </summary>
    public async Task<OcorrenciaRegistrada> RegistrarAsync(NovaOcorrencia nova, CancellationToken ct = default)
    {
        var dispositivo = Motoboy.NormalizarDispositivo(nova.DispositivoId);
        var motoboy = await motoboys.ObterPorDispositivoAsync(dispositivo, ct)
            ?? throw new RecursoNaoEncontradoException($"Dispositivo {dispositivo} não está cadastrado em nenhum motoboy.");

        var ocorrencia = motoboy.RegistrarOcorrencia(nova.Tipo, nova.Latitude, nova.Longitude, nova.IntensidadeG,
                                                     nova.DataHora, relogio.GetUtcNow());
        await motoboys.SalvarAlteracoesAsync(ct); // grava antes de avisar: o registro não se perde se o aviso falhar

        var zona = await ZonaNoLocalAsync(ocorrencia, ct);

        var contatos = motoboy.Contatos.ToList();
        foreach (var contato in contatos)
            foreach (var notificador in notificadores)
                await notificador.NotificarAsync(motoboy, contato, ocorrencia, zona?.Zona, ct);

        ocorrencia.RegistrarNotificacao(contatos.Count, relogio.GetUtcNow());
        await motoboys.SalvarAlteracoesAsync(ct);
        return new OcorrenciaRegistrada(ocorrencia, motoboy, zona, contatos);
    }

    public async Task<Ocorrencia> ObterAsync(int id, CancellationToken ct = default) =>
        await ocorrencias.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException($"Ocorrência {id} não encontrada.");

    public async Task<Ocorrencia> CancelarAsync(int id, CancellationToken ct = default)
    {
        var ocorrencia = await ObterAsync(id, ct);
        ocorrencia.Cancelar(relogio.GetUtcNow());
        await ocorrencias.SalvarAlteracoesAsync(ct);
        return ocorrencia;
    }

    public async Task<Ocorrencia> MarcarAtendidaAsync(int id, CancellationToken ct = default)
    {
        var ocorrencia = await ObterAsync(id, ct);
        ocorrencia.MarcarAtendida(relogio.GetUtcNow());
        await ocorrencias.SalvarAlteracoesAsync(ct);
        return ocorrencia;
    }

    public async Task<IReadOnlyList<Ocorrencia>> HistoricoDoMotoboyAsync(int motoboyId, CancellationToken ct = default)
    {
        _ = await motoboys.ObterPorIdAsync(motoboyId, ct)
            ?? throw new RecursoNaoEncontradoException($"Motoboy {motoboyId} não encontrado.");
        return await ocorrencias.ListarPorMotoboyAsync(motoboyId, ct);
    }

    public async Task<EstatisticasOcorrencias> EstatisticasAsync(CancellationToken ct = default)
    {
        var porTipo = await ocorrencias.ContarPorTipoAsync(ct);
        var porStatus = await ocorrencias.ContarPorStatusAsync(ct);
        var tempos = await ocorrencias.TemposAteAvisoAsync(ct);
        return new EstatisticasOcorrencias(
            porTipo.Values.Sum(), porTipo, porStatus,
            tempos.Count == 0 ? null : Math.Round(tempos.Average(t => t.TotalSeconds), 2),
            tempos.Count == 0 ? null : Math.Round(100.0 * tempos.Count(t => t <= TimeSpan.FromSeconds(30)) / tempos.Count, 1));
    }

    /// <summary>O risco é informação extra para o contato: se o provedor falhar, o socorro segue sem ele.</summary>
    private async Task<ZonaConsultada?> ZonaNoLocalAsync(Ocorrencia o, CancellationToken ct)
    {
        try
        {
            var hora = Fuso.HoraLocal(o.DataHora).Hour;
            var achadas = await zonas.ConsultarAsync(o.Latitude, o.Longitude, hora, 2.0, 1, ct);
            return achadas.Count > 0 ? achadas[0] : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }
}
