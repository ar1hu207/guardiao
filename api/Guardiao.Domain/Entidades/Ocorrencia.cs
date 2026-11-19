namespace Guardiao.Domain.Entidades;

/// <summary>Um pedido de socorro: queda detectada pelo sensor ou SOS manual do app.</summary>
public class Ocorrencia
{
    /// <summary>Abaixo disso o sensor não considera queda (o ESP32 usa o mesmo limiar).</summary>
    public const double LimiarQuedaG = 2.5;

    public int Id { get; private set; }
    public int MotoboyId { get; private set; }
    public TipoOcorrencia Tipo { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? IntensidadeG { get; private set; }

    /// <summary>Quando aconteceu (relógio do dispositivo).</summary>
    public DateTimeOffset DataHora { get; private set; }

    /// <summary>Quando chegou na API.</summary>
    public DateTimeOffset RecebidaEm { get; private set; }

    public StatusOcorrencia Status { get; private set; }
    public DateTimeOffset? NotificadaEm { get; private set; }
    public int ContatosNotificados { get; private set; }
    public DateTimeOffset? EncerradaEm { get; private set; }

    protected Ocorrencia() { } // EF Core

    internal Ocorrencia(TipoOcorrencia tipo, double latitude, double longitude, double? intensidadeG,
                        DateTimeOffset dataHora, DateTimeOffset recebidaEm)
    {
        if (latitude is < -90 or > 90 or double.NaN || longitude is < -180 or > 180 or double.NaN)
            throw new RegraDeNegocioException("Coordenada fora do globo.");
        if (tipo == TipoOcorrencia.QUEDA && (intensidadeG is null || intensidadeG < LimiarQuedaG))
            throw new RegraDeNegocioException($"Uma QUEDA precisa de intensidadeG ≥ {LimiarQuedaG} (veio {intensidadeG?.ToString() ?? "vazio"}).");
        if (intensidadeG is < 0)
            throw new RegraDeNegocioException("intensidadeG não pode ser negativa.");
        if (dataHora > recebidaEm.AddMinutes(5))
            throw new RegraDeNegocioException("dataHora está no futuro: confira o relógio do dispositivo.");

        Tipo = tipo;
        Latitude = latitude;
        Longitude = longitude;
        IntensidadeG = intensidadeG;
        DataHora = dataHora;
        RecebidaEm = recebidaEm;
        Status = StatusOcorrencia.ABERTA;
    }

    /// <summary>Alarme falso: só uma ocorrência ABERTA pode ser cancelada.</summary>
    public void Cancelar(DateTimeOffset agora)
    {
        GarantirAberta("cancelada");
        Status = StatusOcorrencia.CANCELADA;
        EncerradaEm = agora;
    }

    /// <summary>O socorro chegou: só uma ocorrência ABERTA pode ser marcada como atendida.</summary>
    public void MarcarAtendida(DateTimeOffset agora)
    {
        GarantirAberta("atendida");
        Status = StatusOcorrencia.ATENDIDA;
        EncerradaEm = agora;
    }

    public void RegistrarNotificacao(int contatosAvisados, DateTimeOffset agora)
    {
        if (contatosAvisados < 0) throw new RegraDeNegocioException("Quantidade de contatos inválida.");
        ContatosNotificados = contatosAvisados;
        NotificadaEm = agora;
    }

    /// <summary>Indicador de impacto do projeto: tempo entre a queda e o aviso (meta: menos de 30 s).</summary>
    public TimeSpan? TempoAteAviso => NotificadaEm is null ? null : NotificadaEm.Value - DataHora;

    private void GarantirAberta(string acao)
    {
        if (Status != StatusOcorrencia.ABERTA)
            throw new ConflitoException($"A ocorrência {Id} está {Status} e não pode ser {acao}.");
    }
}
