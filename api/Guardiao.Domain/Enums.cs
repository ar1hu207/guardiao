namespace Guardiao.Domain;

/// <summary>De onde veio o pedido de socorro.</summary>
public enum TipoOcorrencia
{
    /// <summary>Queda detectada pelo acelerômetro do ESP32.</summary>
    QUEDA,
    /// <summary>Botão de SOS apertado no app.</summary>
    SOS_MANUAL
}

public enum StatusOcorrencia { ABERTA, CANCELADA, ATENDIDA }

public enum NivelRisco { BAIXO, MEDIO, ALTO }

/// <summary>Faixas do dia usadas pelo Infosiga e pelo modelo de ML.</summary>
public enum FaixaHoraria { MADRUGADA, MANHA, TARDE, NOITE }

public static class FaixaHorariaRegras
{
    /// <summary>0–5 madrugada · 6–11 manhã · 12–17 tarde · 18–23 noite.</summary>
    public static FaixaHoraria DeHora(int hora) => hora switch
    {
        >= 0 and <= 5 => FaixaHoraria.MADRUGADA,
        >= 6 and <= 11 => FaixaHoraria.MANHA,
        >= 12 and <= 17 => FaixaHoraria.TARDE,
        >= 18 and <= 23 => FaixaHoraria.NOITE,
        _ => throw new RegraDeNegocioException($"Hora inválida: {hora}. Use um valor de 0 a 23.")
    };
}

public static class NivelRiscoRegras
{
    public const double LimiteMedio = 0.40;
    public const double LimiteAlto = 0.80;

    /// <summary>Mesmos cortes do notebook de ML: ≥ 0,80 ALTO · ≥ 0,40 MEDIO · abaixo BAIXO.</summary>
    public static NivelRisco DeScore(double score) => score switch
    {
        < 0 or > 1 or double.NaN => throw new RegraDeNegocioException($"Score deve estar entre 0 e 1: {score}"),
        >= LimiteAlto => NivelRisco.ALTO,
        >= LimiteMedio => NivelRisco.MEDIO,
        _ => NivelRisco.BAIXO
    };
}
