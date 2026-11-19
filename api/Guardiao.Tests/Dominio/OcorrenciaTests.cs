using Guardiao.Domain;
using Guardiao.Domain.Entidades;

namespace Guardiao.Tests.Dominio;

public class OcorrenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 15, 23, 41, 0, TimeSpan.FromHours(-3));

    private static Ocorrencia NovaQueda(double g = 4.2) =>
        new(TipoOcorrencia.QUEDA, -23.56, -46.65, g, Agora.AddSeconds(-2), Agora);

    [Fact]
    public void Ocorrencia_nova_comeca_aberta()
    {
        var o = NovaQueda();
        Assert.Equal(StatusOcorrencia.ABERTA, o.Status);
        Assert.Null(o.EncerradaEm);
    }

    [Fact]
    public void Cancelar_ocorrencia_aberta_muda_status_e_registra_horario()
    {
        var o = NovaQueda();
        o.Cancelar(Agora.AddMinutes(1));
        Assert.Equal(StatusOcorrencia.CANCELADA, o.Status);
        Assert.Equal(Agora.AddMinutes(1), o.EncerradaEm);
    }

    [Fact]
    public void Cancelar_duas_vezes_e_conflito()
    {
        var o = NovaQueda();
        o.Cancelar(Agora);
        var erro = Assert.Throws<ConflitoException>(() => o.Cancelar(Agora));
        Assert.Contains("CANCELADA", erro.Message);
    }

    [Fact]
    public void Cancelar_ocorrencia_atendida_e_conflito()
    {
        var o = NovaQueda();
        o.MarcarAtendida(Agora);
        Assert.Throws<ConflitoException>(() => o.Cancelar(Agora));
        Assert.Equal(StatusOcorrencia.ATENDIDA, o.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1.0)]
    [InlineData(2.49)]
    public void Queda_exige_intensidade_minima(double? g)
    {
        Assert.Throws<RegraDeNegocioException>(() =>
            new Ocorrencia(TipoOcorrencia.QUEDA, -23.5, -46.6, g, Agora, Agora));
    }

    [Fact]
    public void Queda_no_limiar_e_aceita() => Assert.Equal(2.5, NovaQueda(Ocorrencia.LimiarQuedaG).IntensidadeG);

    [Fact]
    public void Sos_manual_nao_precisa_de_intensidade()
    {
        var o = new Ocorrencia(TipoOcorrencia.SOS_MANUAL, -23.5, -46.6, null, Agora, Agora);
        Assert.Equal(TipoOcorrencia.SOS_MANUAL, o.Tipo);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    public void Coordenada_fora_do_globo_e_rejeitada(double lat, double lng)
    {
        Assert.Throws<RegraDeNegocioException>(() =>
            new Ocorrencia(TipoOcorrencia.SOS_MANUAL, lat, lng, null, Agora, Agora));
    }

    [Fact]
    public void DataHora_no_futuro_e_rejeitada()
    {
        Assert.Throws<RegraDeNegocioException>(() =>
            new Ocorrencia(TipoOcorrencia.SOS_MANUAL, -23.5, -46.6, null, Agora.AddMinutes(10), Agora));
    }

    [Fact]
    public void Tempo_ate_aviso_e_medido_a_partir_do_evento()
    {
        var o = NovaQueda(); // aconteceu 2 s antes de chegar
        Assert.Null(o.TempoAteAviso);
        o.RegistrarNotificacao(2, Agora.AddSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(5), o.TempoAteAviso);
        Assert.Equal(2, o.ContatosNotificados);
    }
}
