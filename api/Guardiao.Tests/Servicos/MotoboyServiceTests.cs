using Guardiao.Domain;
using Guardiao.Domain.Servicos;

namespace Guardiao.Tests.Servicos;

public class MotoboyServiceTests
{
    private readonly MotoboyRepositoryFake _repo = new();
    private MotoboyService Servico() => new(_repo, new RelogioFixo(DateTimeOffset.UnixEpoch));

    private static DadosMotoboy Bruno => new("Bruno Teste", "11900000009", "TST9Z99", "ESP32-009");

    [Fact]
    public async Task Cadastro_grava_e_comeca_com_dispositivo_desativado()
    {
        var m = await Servico().CadastrarAsync(Bruno);
        Assert.Single(_repo.Motoboys);
        Assert.False(m.DispositivoAtivo);
        Assert.Equal(1, _repo.Salvamentos);
    }

    [Fact]
    public async Task Placa_ou_dispositivo_repetido_e_conflito()
    {
        await Servico().CadastrarAsync(Bruno);
        await Assert.ThrowsAsync<ConflitoException>(() => Servico().CadastrarAsync(Bruno with { DispositivoId = "ESP32-010" }));
        await Assert.ThrowsAsync<ConflitoException>(() => Servico().CadastrarAsync(Bruno with { Placa = "TST9Z98" }));
    }

    [Fact]
    public async Task Nao_troca_o_dispositivo_no_put()
    {
        var m = await Servico().CadastrarAsync(Bruno);
        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            Servico().AtualizarAsync(m.Id, Bruno with { DispositivoId = "ESP32-777" }));
    }

    [Fact]
    public async Task Motoboy_inexistente_e_404()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico().ObterAsync(99));
    }
}
