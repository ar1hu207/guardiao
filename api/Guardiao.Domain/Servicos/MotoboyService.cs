using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;

namespace Guardiao.Domain.Servicos;

public sealed record DadosMotoboy(string Nome, string Telefone, string Placa, string DispositivoId);
public sealed record DadosContato(string Nome, string Telefone, string Parentesco);

public interface IMotoboyService
{
    Task<Motoboy> CadastrarAsync(DadosMotoboy dados, CancellationToken ct = default);
    Task<IReadOnlyList<Motoboy>> ListarAsync(CancellationToken ct = default);
    Task<Motoboy> ObterAsync(int id, CancellationToken ct = default);
    Task<Motoboy> AtualizarAsync(int id, DadosMotoboy dados, CancellationToken ct = default);
    Task RemoverAsync(int id, CancellationToken ct = default);
    Task<ContatoEmergencia> AdicionarContatoAsync(int motoboyId, DadosContato dados, CancellationToken ct = default);
    Task<IReadOnlyCollection<ContatoEmergencia>> ListarContatosAsync(int motoboyId, CancellationToken ct = default);
    Task RemoverContatoAsync(int motoboyId, int contatoId, CancellationToken ct = default);
    Task<Motoboy> AtivarDispositivoAsync(int motoboyId, CancellationToken ct = default);
    Task<Motoboy> DesativarDispositivoAsync(int motoboyId, CancellationToken ct = default);
}

public sealed class MotoboyService(IMotoboyRepository repositorio, TimeProvider relogio) : IMotoboyService
{
    public async Task<Motoboy> CadastrarAsync(DadosMotoboy dados, CancellationToken ct = default)
    {
        var motoboy = new Motoboy(dados.Nome, dados.Telefone, dados.Placa, dados.DispositivoId, relogio.GetUtcNow());
        await GarantirUnicidadeAsync(motoboy.Placa, motoboy.DispositivoId, null, ct);
        await repositorio.AdicionarAsync(motoboy, ct);
        await repositorio.SalvarAlteracoesAsync(ct);
        return motoboy;
    }

    public Task<IReadOnlyList<Motoboy>> ListarAsync(CancellationToken ct = default) => repositorio.ListarAsync(ct);

    public async Task<Motoboy> ObterAsync(int id, CancellationToken ct = default) =>
        await repositorio.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException($"Motoboy {id} não encontrado.");

    public async Task<Motoboy> AtualizarAsync(int id, DadosMotoboy dados, CancellationToken ct = default)
    {
        var motoboy = await ObterAsync(id, ct);
        if (Motoboy.NormalizarDispositivo(dados.DispositivoId) != motoboy.DispositivoId)
            throw new RegraDeNegocioException("O dispositivoId não pode ser trocado pelo cadastro. Desative e cadastre outro motoboy.");
        await GarantirUnicidadeAsync(Motoboy.NormalizarPlaca(dados.Placa), motoboy.DispositivoId, id, ct);
        motoboy.AtualizarCadastro(dados.Nome, dados.Telefone, dados.Placa);
        await repositorio.SalvarAlteracoesAsync(ct);
        return motoboy;
    }

    public async Task RemoverAsync(int id, CancellationToken ct = default)
    {
        var motoboy = await ObterAsync(id, ct);
        if (motoboy.Ocorrencias.Any(o => o.Status == StatusOcorrencia.ABERTA))
            throw new ConflitoException("O motoboy tem ocorrência aberta. Encerre-a antes de remover o cadastro.");
        repositorio.Remover(motoboy);
        await repositorio.SalvarAlteracoesAsync(ct);
    }

    public async Task<ContatoEmergencia> AdicionarContatoAsync(int motoboyId, DadosContato dados, CancellationToken ct = default)
    {
        var motoboy = await ObterAsync(motoboyId, ct);
        var contato = motoboy.AdicionarContato(dados.Nome, dados.Telefone, dados.Parentesco);
        await repositorio.SalvarAlteracoesAsync(ct);
        return contato;
    }

    public async Task<IReadOnlyCollection<ContatoEmergencia>> ListarContatosAsync(int motoboyId, CancellationToken ct = default) =>
        (await ObterAsync(motoboyId, ct)).Contatos;

    public async Task RemoverContatoAsync(int motoboyId, int contatoId, CancellationToken ct = default)
    {
        var motoboy = await ObterAsync(motoboyId, ct);
        motoboy.RemoverContato(contatoId);
        await repositorio.SalvarAlteracoesAsync(ct);
    }

    public async Task<Motoboy> AtivarDispositivoAsync(int motoboyId, CancellationToken ct = default)
    {
        var motoboy = await ObterAsync(motoboyId, ct);
        motoboy.AtivarDispositivo();
        await repositorio.SalvarAlteracoesAsync(ct);
        return motoboy;
    }

    public async Task<Motoboy> DesativarDispositivoAsync(int motoboyId, CancellationToken ct = default)
    {
        var motoboy = await ObterAsync(motoboyId, ct);
        motoboy.DesativarDispositivo();
        await repositorio.SalvarAlteracoesAsync(ct);
        return motoboy;
    }

    private async Task GarantirUnicidadeAsync(string placa, string dispositivoId, int? ignorar, CancellationToken ct)
    {
        if (await repositorio.PlacaEmUsoAsync(placa, ignorar, ct))
            throw new ConflitoException($"Já existe motoboy com a placa {placa}.");
        if (await repositorio.DispositivoEmUsoAsync(dispositivoId, ignorar, ct))
            throw new ConflitoException($"O dispositivo {dispositivoId} já está vinculado a outro motoboy.");
    }
}
