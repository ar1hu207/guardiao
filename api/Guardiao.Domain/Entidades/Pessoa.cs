using System.Text.RegularExpressions;

namespace Guardiao.Domain.Entidades;

/// <summary>
/// Base abstrata de quem participa do Guardião (motoboy e contato de emergência).
/// Concentra a validação de nome e telefone, que vale para os dois.
/// </summary>
public abstract partial class Pessoa
{
    public int Id { get; protected set; }
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Só dígitos, com DDD (10 ou 11 dígitos) e opcionalmente o 55 do Brasil.</summary>
    public string Telefone { get; private set; } = string.Empty;

    protected Pessoa() { } // EF Core

    protected Pessoa(string nome, string telefone) => AtualizarDadosPessoais(nome, telefone);

    public void AtualizarDadosPessoais(string nome, string telefone)
    {
        nome = (nome ?? string.Empty).Trim();
        if (nome.Length is < 2 or > 120)
            throw new RegraDeNegocioException("O nome deve ter entre 2 e 120 caracteres.");

        Nome = nome;
        Telefone = NormalizarTelefone(telefone);
    }

    /// <summary>Como a pessoa aparece numa mensagem ou log. Cada tipo de pessoa se descreve do seu jeito.</summary>
    public abstract string Descricao();

    public static string NormalizarTelefone(string? telefone)
    {
        var digitos = ApenasDigitos().Replace(telefone ?? string.Empty, string.Empty);
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55"))
            digitos = digitos[2..];
        if (digitos.Length is not (10 or 11))
            throw new RegraDeNegocioException($"Telefone inválido: '{telefone}'. Use DDD + número, ex.: (11) 91234-5678.");
        return digitos;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex ApenasDigitos();
}
