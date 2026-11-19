using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Guardiao.Infrastructure.Notificacao;

/// <summary>Monta o texto do aviso. Comum aos canais; cada canal decide como entregar.</summary>
public abstract class NotificadorBase : INotificador
{
    public abstract string Canal { get; }

    public abstract Task NotificarAsync(Motoboy motoboy, ContatoEmergencia contato, Ocorrencia ocorrencia,
                                        ZonaDeRisco? zona, CancellationToken ct = default);

    public static string MontarMensagem(Motoboy motoboy, Ocorrencia o, ZonaDeRisco? zona)
    {
        var oque = o.Tipo == Domain.TipoOcorrencia.QUEDA ? "sofreu uma QUEDA" : "pediu SOCORRO";
        var mapa = $"https://maps.google.com/?q={o.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{o.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var risco = zona is null ? string.Empty : $" Area de risco {zona.Nivel}.";
        return $"GUARDIAO: {motoboy.Nome} {oque} as {Domain.Fuso.HoraLocal(o.DataHora):HH:mm}.{risco} Local: {mapa} Ligue 192 (SAMU).";
    }
}

/// <summary>Só escreve no log. É o canal da demonstração (não envia nada para ninguém).</summary>
public sealed class NotificadorLog(ILogger<NotificadorLog> log) : NotificadorBase
{
    public override string Canal => "log";

    public override Task NotificarAsync(Motoboy motoboy, ContatoEmergencia contato, Ocorrencia ocorrencia,
                                        ZonaDeRisco? zona, CancellationToken ct = default)
    {
        log.LogWarning("[AVISO -> {Contato}] {Mensagem}", contato.Descricao(), MontarMensagem(motoboy, ocorrencia, zona));
        return Task.CompletedTask;
    }
}

/// <summary>Contrato de um provedor de SMS (Twilio, Zenvia...). Fora da demo, troque pela implementação real.</summary>
public interface IGatewaySms
{
    Task EnviarAsync(string telefoneE164, string texto, CancellationToken ct = default);
}

/// <summary>Gateway falso: guarda as mensagens em memória (útil para testes e demonstração).</summary>
public sealed class GatewaySmsSimulado(ILogger<GatewaySmsSimulado> log) : IGatewaySms
{
    private readonly List<(string Telefone, string Texto)> _enviadas = [];
    public IReadOnlyList<(string Telefone, string Texto)> Enviadas => _enviadas;

    public Task EnviarAsync(string telefoneE164, string texto, CancellationToken ct = default)
    {
        lock (_enviadas) _enviadas.Add((telefoneE164, texto));
        log.LogInformation("[SMS simulado -> {Telefone}] {Texto}", telefoneE164, texto);
        return Task.CompletedTask;
    }
}

/// <summary>Envia SMS ao contato. Respeita o limite de 160 caracteres de um SMS.</summary>
public sealed class NotificadorSms(IGatewaySms gateway) : NotificadorBase
{
    public const int LimiteSms = 160;
    public override string Canal => "sms";

    public override Task NotificarAsync(Motoboy motoboy, ContatoEmergencia contato, Ocorrencia ocorrencia,
                                        ZonaDeRisco? zona, CancellationToken ct = default)
    {
        var texto = MontarMensagem(motoboy, ocorrencia, zona);
        if (texto.Length > LimiteSms) texto = texto[..(LimiteSms - 1)] + "…";
        return gateway.EnviarAsync($"+55{contato.Telefone}", texto, ct);
    }
}
