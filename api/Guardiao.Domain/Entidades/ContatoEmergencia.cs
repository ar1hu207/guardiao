namespace Guardiao.Domain.Entidades;

/// <summary>Quem é avisado quando o motoboy cai. Só existe ligado a um motoboy.</summary>
public class ContatoEmergencia : Pessoa
{
    public string Parentesco { get; private set; } = string.Empty;
    public int MotoboyId { get; private set; }

    protected ContatoEmergencia() { } // EF Core

    internal ContatoEmergencia(string nome, string telefone, string parentesco) : base(nome, telefone)
    {
        parentesco = (parentesco ?? string.Empty).Trim();
        if (parentesco.Length is < 2 or > 40)
            throw new RegraDeNegocioException("Informe o parentesco (ex.: mãe, irmão, amigo).");
        Parentesco = parentesco;
    }

    public override string Descricao() => $"{Nome} ({Parentesco})";
}
