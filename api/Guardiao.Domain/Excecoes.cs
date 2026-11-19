namespace Guardiao.Domain;

/// <summary>Base dos erros de negócio. A API traduz cada tipo para um código HTTP.</summary>
public abstract class DominioException(string mensagem) : Exception(mensagem);

/// <summary>Dado inválido segundo uma regra do domínio (HTTP 400).</summary>
public sealed class RegraDeNegocioException(string mensagem) : DominioException(mensagem);

/// <summary>Recurso inexistente (HTTP 404).</summary>
public sealed class RecursoNaoEncontradoException(string mensagem) : DominioException(mensagem);

/// <summary>A operação conflita com o estado atual (HTTP 409). Ex.: cancelar ocorrência já cancelada.</summary>
public sealed class ConflitoException(string mensagem) : DominioException(mensagem);
