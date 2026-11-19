using System.ComponentModel.DataAnnotations;
using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;
using Guardiao.Domain.Servicos;

namespace Guardiao.Api;

// ---------- Entrada ----------

/// <summary>Corpo do POST /api/ocorrencias (contrato combinado com o ESP32 e o app).</summary>
public sealed record OcorrenciaRequest
{
    /// <example>ESP32-001</example>
    [Required] public string DispositivoId { get; init; } = string.Empty;
    /// <summary>QUEDA (veio do sensor) ou SOS_MANUAL (veio do botão do app).</summary>
    /// <example>QUEDA</example>
    [Required] public TipoOcorrencia? Tipo { get; init; }
    /// <example>-23.5614</example>
    [Required, Range(-90, 90)] public double? Latitude { get; init; }
    /// <example>-46.6559</example>
    [Required, Range(-180, 180)] public double? Longitude { get; init; }
    /// <summary>Pico de aceleração medido pelo sensor, em g. Obrigatório para QUEDA (≥ 2,5).</summary>
    /// <example>4.2</example>
    public double? IntensidadeG { get; init; }
    /// <summary>Quando aconteceu, com fuso (ex.: 2026-11-15T23:41:00-03:00). Sem valor, vale a hora em que chegou.</summary>
    public DateTimeOffset? DataHora { get; init; }
}

public sealed record MotoboyRequest
{
    /// <example>Bruno Teste Rocha</example>
    [Required] public string Nome { get; init; } = string.Empty;
    /// <example>(11) 90000-0009</example>
    [Required] public string Telefone { get; init; } = string.Empty;
    /// <summary>ABC1234 ou ABC1D23.</summary>
    /// <example>TST9Z99</example>
    [Required] public string Placa { get; init; } = string.Empty;
    /// <example>ESP32-009</example>
    [Required] public string DispositivoId { get; init; } = string.Empty;
}

public sealed record ContatoRequest
{
    /// <example>Joana Teste Rocha</example>
    [Required] public string Nome { get; init; } = string.Empty;
    /// <example>(11) 90000-9001</example>
    [Required] public string Telefone { get; init; } = string.Empty;
    /// <example>esposa</example>
    [Required] public string Parentesco { get; init; } = string.Empty;
}

// ---------- Saída ----------

public sealed record ContatoResponse(int Id, string Nome, string Telefone, string Parentesco)
{
    public static ContatoResponse De(ContatoEmergencia c) => new(c.Id, c.Nome, c.Telefone, c.Parentesco);
}

public sealed record MotoboyResponse(int Id, string Nome, string Telefone, string Placa, string DispositivoId,
                                     bool DispositivoAtivo, DateTimeOffset CadastradoEm, IReadOnlyList<ContatoResponse> Contatos)
{
    public static MotoboyResponse De(Motoboy m) => new(m.Id, m.Nome, m.Telefone, m.Placa, m.DispositivoId, m.DispositivoAtivo,
        m.CadastradoEm, m.Contatos.Select(ContatoResponse.De).ToList());
}

public sealed record OcorrenciaResponse(int Id, int MotoboyId, TipoOcorrencia Tipo, StatusOcorrencia Status, double Latitude,
                                        double Longitude, double? IntensidadeG, DateTimeOffset DataHora, DateTimeOffset RecebidaEm,
                                        DateTimeOffset? NotificadaEm, int ContatosNotificados, double? SegundosAteAviso,
                                        DateTimeOffset? EncerradaEm)
{
    public static OcorrenciaResponse De(Ocorrencia o) => new(o.Id, o.MotoboyId, o.Tipo, o.Status, o.Latitude, o.Longitude,
        o.IntensidadeG, o.DataHora, o.RecebidaEm, o.NotificadaEm, o.ContatosNotificados,
        o.TempoAteAviso is { } t ? Math.Round(t.TotalSeconds, 2) : null, o.EncerradaEm);
}

/// <summary>Formato do contrato: { id, nome, nivel, score } + extras.</summary>
public sealed record ZonaRiscoResponse(int Id, string Nome, NivelRisco Nivel, double Score, FaixaHoraria FaixaHoraria,
                                       double Latitude, double Longitude, double DistanciaKm)
{
    public static ZonaRiscoResponse De(ZonaConsultada r) => new(r.Zona.ZonaId, r.Zona.Nome, r.Zona.Nivel, r.Zona.Score,
        r.Zona.FaixaHoraria, r.Zona.Latitude, r.Zona.Longitude, r.DistanciaKm);
}

public sealed record OcorrenciaRegistradaResponse(OcorrenciaResponse Ocorrencia, string Motoboy,
                                                  IReadOnlyList<string> ContatosAvisados, ZonaRiscoResponse? RiscoNoLocal)
{
    public static OcorrenciaRegistradaResponse De(OcorrenciaRegistrada r) => new(
        OcorrenciaResponse.De(r.Ocorrencia), r.Motoboy.Descricao(),
        r.ContatosAvisados.Select(c => c.Descricao()).ToList(),
        r.ZonaNoLocal is null ? null : ZonaRiscoResponse.De(r.ZonaNoLocal));
}

public sealed record EstatisticasResponse(int Total, IReadOnlyDictionary<string, int> PorTipo, IReadOnlyDictionary<string, int> PorStatus,
                                          double? TempoMedioAteAvisoSegundos, double? PercentualAvisoEmAte30s)
{
    public static EstatisticasResponse De(EstatisticasOcorrencias e) => new(e.Total,
        e.PorTipo.ToDictionary(k => k.Key.ToString(), v => v.Value),
        e.PorStatus.ToDictionary(k => k.Key.ToString(), v => v.Value),
        e.TempoMedioAteAvisoSegundos, e.PercentualAvisoEmAte30s);
}
