using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;
using Guardiao.Domain.Servicos;
using Guardiao.Infrastructure.Notificacao;
using Microsoft.Extensions.Logging.Abstractions;

namespace Guardiao.Tests.Servicos;

public class OcorrenciaServiceTests
{
    // 23h41 em São Paulo = 02h41 UTC do dia seguinte: a faixa tem de sair do fuso local (NOITE), não do UTC.
    private static readonly DateTimeOffset Agora = new(2026, 11, 15, 23, 41, 0, TimeSpan.FromHours(-3));

    private readonly MotoboyRepositoryFake _motoboys = new();
    private readonly RelogioFixo _relogio = new(Agora);

    private Motoboy CadastrarJoao(bool ativo = true)
    {
        var m = new Motoboy("João Teste", "11900000001", "TST1A01", "ESP32-001", Agora);
        m.AdicionarContato("Maria Teste", "11900001001", "mãe");
        m.AdicionarContato("Pedro Teste", "11900001002", "irmão");
        if (ativo) m.AtivarDispositivo();
        _motoboys.Motoboys.Add(m);
        return m;
    }

    private OcorrenciaService Servico(IZonaRiscoProvider? zonas = null, params INotificador[] notificadores) =>
        new(_motoboys, new OcorrenciaRepositoryFake(_motoboys), zonas ?? new ZonaRiscoProviderFake(), notificadores, _relogio);

    private static NovaOcorrencia Queda(string dispositivo = "ESP32-001") =>
        new(dispositivo, TipoOcorrencia.QUEDA, -23.59, -46.79, 4.2, Agora.AddSeconds(-1));

    [Fact]
    public async Task Dispositivo_desconhecido_e_404_e_nada_e_gravado()
    {
        CadastrarJoao();
        var sms = new NotificadorEspiao("sms");

        var erro = await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico(null, sms).RegistrarAsync(Queda("ESP32-999")));

        Assert.Contains("ESP32-999", erro.Message);
        Assert.Equal(0, _motoboys.Salvamentos);
        Assert.Empty(sms.Avisos);
    }

    [Fact]
    public async Task Dispositivo_e_achado_mesmo_em_minusculas()
    {
        CadastrarJoao();
        var r = await Servico().RegistrarAsync(Queda(" esp32-001 "));
        Assert.Equal("João Teste", r.Motoboy.Nome);
    }

    [Fact]
    public async Task Avisa_cada_contato_em_cada_canal_com_o_risco_do_local()
    {
        CadastrarJoao();
        var zona = new ZonaDeRisco(78, "SP-270 e entorno", -23.59, -46.79, FaixaHoraria.NOITE, 0.88);
        var sms = new NotificadorEspiao("sms");
        var log = new NotificadorEspiao("log");
        _relogio.Agora = Agora.AddSeconds(2);

        var r = await Servico(new ZonaRiscoProviderFake(zona), sms, log).RegistrarAsync(Queda());

        Assert.Equal(["Maria Teste", "Pedro Teste"], sms.Avisos.Select(a => a.Contato));
        Assert.Equal(2, log.Avisos.Count);
        Assert.All(sms.Avisos, a => Assert.Equal(NivelRisco.ALTO, a.Risco));
        Assert.Equal(2, r.Ocorrencia.ContatosNotificados);
        Assert.Equal(TimeSpan.FromSeconds(3), r.Ocorrencia.TempoAteAviso);
        Assert.Equal(78, r.ZonaNoLocal?.Zona.ZonaId);
        Assert.Equal(StatusOcorrencia.ABERTA, r.Ocorrencia.Status);
    }

    [Fact]
    public async Task Hora_do_risco_e_a_de_Sao_Paulo()
    {
        CadastrarJoao();
        var zonas = new ZonaRiscoProviderFake();
        await Servico(zonas).RegistrarAsync(Queda());
        Assert.Equal([23], zonas.HorasConsultadas);
    }

    [Fact]
    public async Task Servico_de_risco_fora_do_ar_nao_impede_o_socorro()
    {
        CadastrarJoao();
        var sms = new NotificadorEspiao("sms");

        var r = await Servico(new ZonaRiscoProviderFake(falhar: true), sms).RegistrarAsync(Queda());

        Assert.Null(r.ZonaNoLocal);
        Assert.Equal(2, sms.Avisos.Count);
    }

    [Fact]
    public async Task Queda_com_dispositivo_desativado_e_conflito()
    {
        CadastrarJoao(ativo: false);
        await Assert.ThrowsAsync<ConflitoException>(() => Servico().RegistrarAsync(Queda()));
    }

    [Fact]
    public async Task Cancelar_ocorrencia_inexistente_e_404()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico().CancelarAsync(123));
    }

    [Fact]
    public async Task Sms_real_do_projeto_monta_a_mensagem_no_horario_local()
    {
        CadastrarJoao();
        var gateway = new GatewaySmsSimulado(NullLogger<GatewaySmsSimulado>.Instance);
        var zona = new ZonaDeRisco(78, "SP-270 e entorno", -23.59, -46.79, FaixaHoraria.NOITE, 0.88);

        await Servico(new ZonaRiscoProviderFake(zona), new NotificadorSms(gateway)).RegistrarAsync(Queda());

        Assert.Equal(["+5511900001001", "+5511900001002"], gateway.Enviadas.Select(e => e.Telefone));
        var texto = gateway.Enviadas[0].Texto;
        Assert.Contains("João Teste sofreu uma QUEDA as 23:40", texto);
        Assert.Contains("Area de risco ALTO", texto);
        Assert.True(texto.Length <= 160);
    }

    [Fact]
    public async Task Estatisticas_contam_por_tipo_e_status_com_linq()
    {
        var joao = CadastrarJoao();
        var servico = Servico();
        await servico.RegistrarAsync(Queda());
        await servico.RegistrarAsync(Queda() with { Tipo = TipoOcorrencia.SOS_MANUAL, IntensidadeG = null });
        joao.Ocorrencias.First().Cancelar(Agora);

        var e = await servico.EstatisticasAsync();

        Assert.Equal(2, e.Total);
        Assert.Equal(1, e.PorTipo[TipoOcorrencia.QUEDA]);
        Assert.Equal(1, e.PorTipo[TipoOcorrencia.SOS_MANUAL]);
        Assert.Equal(1, e.PorStatus[StatusOcorrencia.CANCELADA]);
        Assert.Equal(100, e.PercentualAvisoEmAte30s);
    }
}
