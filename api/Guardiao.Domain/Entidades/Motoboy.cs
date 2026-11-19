using System.Text.RegularExpressions;

namespace Guardiao.Domain.Entidades;

/// <summary>
/// Entregador de moto. É a raiz do agregado: contatos e ocorrências só entram por aqui,
/// para que as regras (limite de contatos, dispositivo ativo, etc.) nunca sejam furadas.
/// </summary>
public partial class Motoboy : Pessoa
{
    public const int MaximoContatos = 5;

    private readonly List<ContatoEmergencia> _contatos = [];
    private readonly List<Ocorrencia> _ocorrencias = [];

    public string Placa { get; private set; } = string.Empty;

    /// <summary>Id da caixinha ESP32 presa na moto (ex.: ESP32-001).</summary>
    public string DispositivoId { get; private set; } = string.Empty;

    public bool DispositivoAtivo { get; private set; }
    public DateTimeOffset CadastradoEm { get; private set; }

    public IReadOnlyCollection<ContatoEmergencia> Contatos => _contatos.AsReadOnly();
    public IReadOnlyCollection<Ocorrencia> Ocorrencias => _ocorrencias.AsReadOnly();

    protected Motoboy() { } // EF Core

    public Motoboy(string nome, string telefone, string placa, string dispositivoId, DateTimeOffset cadastradoEm)
        : base(nome, telefone)
    {
        Placa = NormalizarPlaca(placa);
        DispositivoId = NormalizarDispositivo(dispositivoId);
        CadastradoEm = cadastradoEm;
    }

    public void AtualizarCadastro(string nome, string telefone, string placa)
    {
        AtualizarDadosPessoais(nome, telefone);
        Placa = NormalizarPlaca(placa);
    }

    public ContatoEmergencia AdicionarContato(string nome, string telefone, string parentesco)
    {
        if (_contatos.Count >= MaximoContatos)
            throw new ConflitoException($"O motoboy já tem {MaximoContatos} contatos de emergência.");

        var contato = new ContatoEmergencia(nome, telefone, parentesco);
        if (contato.Telefone == Telefone)
            throw new RegraDeNegocioException("O contato de emergência não pode ser o próprio motoboy.");
        if (_contatos.Any(c => c.Telefone == contato.Telefone))
            throw new ConflitoException("Já existe um contato com este telefone.");

        _contatos.Add(contato);
        return contato;
    }

    public void RemoverContato(int contatoId)
    {
        var contato = _contatos.FirstOrDefault(c => c.Id == contatoId)
            ?? throw new RecursoNaoEncontradoException($"Contato {contatoId} não encontrado para o motoboy {Id}.");
        if (DispositivoAtivo && _contatos.Count == 1)
            throw new ConflitoException("Com o dispositivo ativo, o motoboy precisa manter pelo menos um contato.");
        _contatos.Remove(contato);
    }

    /// <summary>Sem ninguém para avisar, o dispositivo não tem para quem pedir socorro.</summary>
    public void AtivarDispositivo()
    {
        if (_contatos.Count == 0)
            throw new ConflitoException("Cadastre pelo menos um contato de emergência antes de ativar o dispositivo.");
        DispositivoAtivo = true;
    }

    public void DesativarDispositivo() => DispositivoAtivo = false;

    /// <summary>Registra o pedido de socorro. QUEDA só vale com o dispositivo ativo; SOS manual vale sempre.</summary>
    public Ocorrencia RegistrarOcorrencia(TipoOcorrencia tipo, double latitude, double longitude, double? intensidadeG,
                                          DateTimeOffset dataHora, DateTimeOffset recebidaEm)
    {
        if (tipo == TipoOcorrencia.QUEDA && !DispositivoAtivo)
            throw new ConflitoException($"O dispositivo {DispositivoId} está desativado.");

        var ocorrencia = new Ocorrencia(tipo, latitude, longitude, intensidadeG, dataHora, recebidaEm);
        _ocorrencias.Add(ocorrencia);
        return ocorrencia;
    }

    public override string Descricao() => $"{Nome} (placa {Placa})";

    /// <summary>Placa antiga (ABC1234) ou Mercosul (ABC1D23).</summary>
    public static string NormalizarPlaca(string? placa)
    {
        var p = (placa ?? string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();
        if (!PlacaValida().IsMatch(p))
            throw new RegraDeNegocioException($"Placa inválida: '{placa}'. Use ABC1234 ou ABC1D23.");
        return p;
    }

    public static string NormalizarDispositivo(string? dispositivoId)
    {
        var d = (dispositivoId ?? string.Empty).Trim().ToUpperInvariant();
        if (d.Length is < 3 or > 40)
            throw new RegraDeNegocioException("dispositivoId deve ter entre 3 e 40 caracteres.");
        return d;
    }

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$")]
    private static partial Regex PlacaValida();
}
