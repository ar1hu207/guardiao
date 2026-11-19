using Guardiao.Domain.Entidades;

namespace Guardiao.Domain.Interfaces;

public interface IMotoboyRepository
{
    Task<Motoboy?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<Motoboy?> ObterPorDispositivoAsync(string dispositivoId, CancellationToken ct = default);
    Task<IReadOnlyList<Motoboy>> ListarAsync(CancellationToken ct = default);
    Task<bool> DispositivoEmUsoAsync(string dispositivoId, int? ignorarMotoboyId = null, CancellationToken ct = default);
    Task<bool> PlacaEmUsoAsync(string placa, int? ignorarMotoboyId = null, CancellationToken ct = default);
    Task AdicionarAsync(Motoboy motoboy, CancellationToken ct = default);
    void Remover(Motoboy motoboy);
    Task SalvarAlteracoesAsync(CancellationToken ct = default);
}

public interface IOcorrenciaRepository
{
    Task<Ocorrencia?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Ocorrencia>> ListarPorMotoboyAsync(int motoboyId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<TipoOcorrencia, int>> ContarPorTipoAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<StatusOcorrencia, int>> ContarPorStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TimeSpan>> TemposAteAvisoAsync(CancellationToken ct = default);
    Task SalvarAlteracoesAsync(CancellationToken ct = default);
}

/// <summary>Resultado de uma consulta de risco: a zona e a distância até o ponto consultado.</summary>
public sealed record ZonaConsultada(ZonaDeRisco Zona, double DistanciaKm);

/// <summary>
/// De onde vem o risco por região. Duas implementações: o banco local (CSV do ML carregado na inicialização)
/// ou o Serviço de Risco remoto (SOA), escolhidas na configuração.
/// </summary>
public interface IZonaRiscoProvider
{
    string Origem { get; }
    Task<IReadOnlyList<ZonaConsultada>> ConsultarAsync(double latitude, double longitude, int hora, double raioKm,
                                                       int limite, CancellationToken ct = default);
}

/// <summary>Canal de aviso aos contatos de emergência (SMS, log, WhatsApp...).</summary>
public interface INotificador
{
    string Canal { get; }
    Task NotificarAsync(Motoboy motoboy, ContatoEmergencia contato, Ocorrencia ocorrencia, ZonaDeRisco? zona,
                        CancellationToken ct = default);
}
