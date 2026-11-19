using Guardiao.Domain;
using Guardiao.Domain.Entidades;

namespace Guardiao.Tests.Dominio;

public class MotoboyTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static Motoboy NovoMotoboy() => new("Bruno Teste", "(11) 90000-0009", "tst9z99", " esp32-009 ", Agora);

    [Fact]
    public void Construtor_normaliza_placa_telefone_e_dispositivo()
    {
        var m = NovoMotoboy();
        Assert.Equal("TST9Z99", m.Placa);
        Assert.Equal("11900000009", m.Telefone);
        Assert.Equal("ESP32-009", m.DispositivoId);
        Assert.False(m.DispositivoAtivo);
    }

    [Fact]
    public void Motoboy_e_contato_sao_pessoas() // herança: os dois vêm da classe abstrata Pessoa
    {
        var m = NovoMotoboy();
        var c = m.AdicionarContato("Joana Teste", "11 90000-9001", "esposa");
        Pessoa[] pessoas = [m, c];
        Assert.Equal(["Bruno Teste (placa TST9Z99)", "Joana Teste (esposa)"], pessoas.Select(p => p.Descricao()));
    }

    [Theory]
    [InlineData("ABC1234", "ABC1234")] // modelo antigo
    [InlineData("abc-1d23", "ABC1D23")] // Mercosul
    public void Placa_valida(string entrada, string esperado) => Assert.Equal(esperado, Motoboy.NormalizarPlaca(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("AB12345")]
    [InlineData("ABCD123")]
    [InlineData(null)]
    public void Placa_invalida(string? placa) => Assert.Throws<RegraDeNegocioException>(() => Motoboy.NormalizarPlaca(placa));

    [Theory]
    [InlineData("+55 (11) 90000-0001", "11900000001")]
    [InlineData("1133334444", "1133334444")]
    public void Telefone_valido(string entrada, string esperado) => Assert.Equal(esperado, Pessoa.NormalizarTelefone(entrada));

    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    [InlineData("")]
    public void Telefone_invalido(string telefone) => Assert.Throws<RegraDeNegocioException>(() => Pessoa.NormalizarTelefone(telefone));

    [Fact]
    public void Nao_ativa_dispositivo_sem_contato()
    {
        var m = NovoMotoboy();
        Assert.Throws<ConflitoException>(m.AtivarDispositivo);
        m.AdicionarContato("Joana Teste", "11900009001", "esposa");
        m.AtivarDispositivo();
        Assert.True(m.DispositivoAtivo);
    }

    [Fact]
    public void Limite_de_cinco_contatos()
    {
        var m = NovoMotoboy();
        for (var i = 1; i <= Motoboy.MaximoContatos; i++)
            m.AdicionarContato($"Contato {i}", $"1190000900{i}", "amigo");
        Assert.Throws<ConflitoException>(() => m.AdicionarContato("Sexto", "11900009999", "amigo"));
    }

    [Fact]
    public void Contato_com_telefone_repetido_e_conflito()
    {
        var m = NovoMotoboy();
        m.AdicionarContato("Joana", "11900009001", "esposa");
        Assert.Throws<ConflitoException>(() => m.AdicionarContato("Outra", "(11) 90000-9001", "irmã"));
    }

    [Fact]
    public void Motoboy_nao_pode_ser_o_proprio_contato()
    {
        var m = NovoMotoboy();
        Assert.Throws<RegraDeNegocioException>(() => m.AdicionarContato("Eu mesmo", "11900000009", "eu"));
    }

    [Fact]
    public void Queda_com_dispositivo_desativado_e_conflito_mas_sos_passa()
    {
        var m = NovoMotoboy();
        Assert.Throws<ConflitoException>(() =>
            m.RegistrarOcorrencia(TipoOcorrencia.QUEDA, -23.5, -46.6, 4, Agora, Agora));

        var sos = m.RegistrarOcorrencia(TipoOcorrencia.SOS_MANUAL, -23.5, -46.6, null, Agora, Agora);
        Assert.Single(m.Ocorrencias);
        Assert.Equal(StatusOcorrencia.ABERTA, sos.Status);
    }

    [Fact]
    public void Remover_contato_inexistente_e_404()
    {
        var m = NovoMotoboy();
        Assert.Throws<RecursoNaoEncontradoException>(() => m.RemoverContato(42));
    }
}
