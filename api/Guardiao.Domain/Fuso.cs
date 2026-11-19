namespace Guardiao.Domain;

/// <summary>O Guardião opera em São Paulo: faixa horária do risco e hora da mensagem usam o fuso local.</summary>
public static class Fuso
{
    public static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset HoraLocal(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, SaoPaulo);
}
