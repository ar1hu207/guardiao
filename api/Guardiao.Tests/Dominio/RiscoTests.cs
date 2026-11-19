using Guardiao.Domain;
using Guardiao.Domain.Entidades;

namespace Guardiao.Tests.Dominio;

public class RiscoTests
{
    [Theory]
    [InlineData(0, FaixaHoraria.MADRUGADA)]
    [InlineData(5, FaixaHoraria.MADRUGADA)]
    [InlineData(6, FaixaHoraria.MANHA)]
    [InlineData(12, FaixaHoraria.TARDE)]
    [InlineData(17, FaixaHoraria.TARDE)]
    [InlineData(18, FaixaHoraria.NOITE)]
    [InlineData(23, FaixaHoraria.NOITE)]
    public void Faixa_horaria_bate_com_o_ml(int hora, FaixaHoraria esperada) =>
        Assert.Equal(esperada, FaixaHorariaRegras.DeHora(hora));

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void Hora_fora_do_dia_e_rejeitada(int hora) =>
        Assert.Throws<RegraDeNegocioException>(() => FaixaHorariaRegras.DeHora(hora));

    [Theory]
    [InlineData(0.0, NivelRisco.BAIXO)]
    [InlineData(0.39, NivelRisco.BAIXO)]
    [InlineData(0.40, NivelRisco.MEDIO)]
    [InlineData(0.79, NivelRisco.MEDIO)]
    [InlineData(0.80, NivelRisco.ALTO)]
    [InlineData(1.0, NivelRisco.ALTO)]
    public void Nivel_derivado_do_score(double score, NivelRisco esperado) =>
        Assert.Equal(esperado, new ZonaDeRisco(1, "Zona", -23.5, -46.6, FaixaHoraria.NOITE, score).Nivel);

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Score_fora_de_0_a_1_e_rejeitado(double score) =>
        Assert.Throws<RegraDeNegocioException>(() => NivelRiscoRegras.DeScore(score));

    [Fact]
    public void Haversine_paulista_ate_se_tem_cerca_de_2_5_km()
    {
        // MASP (-23.5614, -46.6559) até a Praça da Sé (-23.5503, -46.6340)
        var km = ZonaDeRisco.Haversine(-23.5614, -46.6559, -23.5503, -46.6340);
        Assert.InRange(km, 2.3, 2.7);
    }
}
