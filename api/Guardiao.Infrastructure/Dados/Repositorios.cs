using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Dados;

public sealed class MotoboyRepository(GuardiaoDbContext db) : IMotoboyRepository
{
    private IQueryable<Motoboy> ComContatos => db.Motoboys.Include(m => m.Contatos);

    public Task<Motoboy?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        ComContatos.Include(m => m.Ocorrencias).AsSplitQuery().FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Motoboy?> ObterPorDispositivoAsync(string dispositivoId, CancellationToken ct = default) =>
        ComContatos.FirstOrDefaultAsync(m => m.DispositivoId == dispositivoId, ct);

    public async Task<IReadOnlyList<Motoboy>> ListarAsync(CancellationToken ct = default) =>
        await ComContatos.AsNoTracking().OrderBy(m => m.Nome).ToListAsync(ct);

    public Task<bool> DispositivoEmUsoAsync(string dispositivoId, int? ignorarMotoboyId = null, CancellationToken ct = default) =>
        db.Motoboys.AnyAsync(m => m.DispositivoId == dispositivoId && m.Id != ignorarMotoboyId, ct);

    public Task<bool> PlacaEmUsoAsync(string placa, int? ignorarMotoboyId = null, CancellationToken ct = default) =>
        db.Motoboys.AnyAsync(m => m.Placa == placa && m.Id != ignorarMotoboyId, ct);

    public async Task AdicionarAsync(Motoboy motoboy, CancellationToken ct = default) => await db.Motoboys.AddAsync(motoboy, ct);

    public void Remover(Motoboy motoboy) => db.Motoboys.Remove(motoboy);

    public Task SalvarAlteracoesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public sealed class OcorrenciaRepository(GuardiaoDbContext db) : IOcorrenciaRepository
{
    public Task<Ocorrencia?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Ocorrencias.FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <summary>Histórico do motoboy, da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<Ocorrencia>> ListarPorMotoboyAsync(int motoboyId, CancellationToken ct = default) =>
        await db.Ocorrencias.AsNoTracking()
            .Where(o => o.MotoboyId == motoboyId)
            .OrderByDescending(o => o.DataHora)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<TipoOcorrencia, int>> ContarPorTipoAsync(CancellationToken ct = default) =>
        await db.Ocorrencias.GroupBy(o => o.Tipo)
            .Select(g => new { Tipo = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Tipo, x => x.Total, ct);

    public async Task<IReadOnlyDictionary<StatusOcorrencia, int>> ContarPorStatusAsync(CancellationToken ct = default) =>
        await db.Ocorrencias.GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Total, ct);

    public async Task<IReadOnlyList<TimeSpan>> TemposAteAvisoAsync(CancellationToken ct = default)
    {
        var pares = await db.Ocorrencias.AsNoTracking()
            .Where(o => o.NotificadaEm != null)
            .Select(o => new { o.DataHora, o.NotificadaEm })
            .ToListAsync(ct);
        return pares.Select(p => p.NotificadaEm!.Value - p.DataHora).ToList();
    }

    public Task SalvarAlteracoesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
