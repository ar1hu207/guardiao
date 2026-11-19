using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;

namespace Guardiao.Tests.Servicos;

/// <summary>Relógio parado: deixa os testes determinísticos.</summary>
public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = agora;
    public override DateTimeOffset GetUtcNow() => Agora.ToUniversalTime();
}

/// <summary>Repositório em memória: o serviço só conhece a interface, então o teste não precisa de banco.</summary>
public sealed class MotoboyRepositoryFake : IMotoboyRepository
{
    public List<Motoboy> Motoboys { get; } = [];
    public int Salvamentos { get; private set; }

    public Task<Motoboy?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Motoboys.FirstOrDefault(m => m.Id == id));

    public Task<Motoboy?> ObterPorDispositivoAsync(string dispositivoId, CancellationToken ct = default) =>
        Task.FromResult(Motoboys.FirstOrDefault(m => m.DispositivoId == dispositivoId));

    public Task<IReadOnlyList<Motoboy>> ListarAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Motoboy>>(Motoboys.OrderBy(m => m.Nome).ToList());

    public Task<bool> DispositivoEmUsoAsync(string dispositivoId, int? ignorarMotoboyId = null, CancellationToken ct = default) =>
        Task.FromResult(Motoboys.Any(m => m.DispositivoId == dispositivoId && m.Id != ignorarMotoboyId));

    public Task<bool> PlacaEmUsoAsync(string placa, int? ignorarMotoboyId = null, CancellationToken ct = default) =>
        Task.FromResult(Motoboys.Any(m => m.Placa == placa && m.Id != ignorarMotoboyId));

    public Task AdicionarAsync(Motoboy motoboy, CancellationToken ct = default)
    {
        Motoboys.Add(motoboy);
        return Task.CompletedTask;
    }

    public void Remover(Motoboy motoboy) => Motoboys.Remove(motoboy);

    public Task SalvarAlteracoesAsync(CancellationToken ct = default)
    {
        Salvamentos++;
        return Task.CompletedTask;
    }
}

public sealed class OcorrenciaRepositoryFake(MotoboyRepositoryFake motoboys) : IOcorrenciaRepository
{
    private IEnumerable<Ocorrencia> Todas => motoboys.Motoboys.SelectMany(m => m.Ocorrencias);

    public Task<Ocorrencia?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Todas.FirstOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<Ocorrencia>> ListarPorMotoboyAsync(int motoboyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Ocorrencia>>(Todas.Where(o => o.MotoboyId == motoboyId).ToList());

    public Task<IReadOnlyDictionary<TipoOcorrencia, int>> ContarPorTipoAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<TipoOcorrencia, int>>(Todas.GroupBy(o => o.Tipo).ToDictionary(g => g.Key, g => g.Count()));

    public Task<IReadOnlyDictionary<StatusOcorrencia, int>> ContarPorStatusAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<StatusOcorrencia, int>>(Todas.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<IReadOnlyList<TimeSpan>> TemposAteAvisoAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TimeSpan>>(Todas.Where(o => o.TempoAteAviso is not null).Select(o => o.TempoAteAviso!.Value).ToList());

    public Task SalvarAlteracoesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class ZonaRiscoProviderFake(ZonaDeRisco? zona = null, bool falhar = false) : IZonaRiscoProvider
{
    public string Origem => "fake";
    public List<int> HorasConsultadas { get; } = [];

    public Task<IReadOnlyList<ZonaConsultada>> ConsultarAsync(double latitude, double longitude, int hora, double raioKm,
        int limite, CancellationToken ct = default)
    {
        HorasConsultadas.Add(hora);
        if (falhar) throw new HttpRequestException("Serviço de Risco fora do ar");
        IReadOnlyList<ZonaConsultada> r = zona is null ? [] : [new ZonaConsultada(zona, 0.3)];
        return Task.FromResult(r);
    }
}

/// <summary>Notificador espião: guarda quem foi avisado.</summary>
public sealed class NotificadorEspiao(string canal) : INotificador
{
    public string Canal => canal;
    public List<(string Contato, NivelRisco? Risco)> Avisos { get; } = [];

    public Task NotificarAsync(Motoboy motoboy, ContatoEmergencia contato, Ocorrencia ocorrencia, ZonaDeRisco? zona,
        CancellationToken ct = default)
    {
        Avisos.Add((contato.Nome, zona?.Nivel));
        return Task.CompletedTask;
    }
}
