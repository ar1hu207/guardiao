using System.Globalization;
using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Guardiao.Infrastructure.Dados;

/// <summary>Aplica as migrações, carrega as zonas do ML e cria motoboys de demonstração (dados inventados).</summary>
public sealed class CargaInicial(GuardiaoDbContext db, TimeProvider relogio, ILogger<CargaInicial> log)
{
    public async Task ExecutarAsync(string? caminhoCsvZonas, bool criarMotoboysDemo, CancellationToken ct = default)
    {
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync(ct);

        if (!await db.ZonasDeRisco.AnyAsync(ct) && caminhoCsvZonas is not null && File.Exists(caminhoCsvZonas))
        {
            var zonas = LerZonasCsv(await File.ReadAllLinesAsync(caminhoCsvZonas, ct));
            await db.ZonasDeRisco.AddRangeAsync(zonas, ct);
            await db.SaveChangesAsync(ct);
            log.LogInformation("Zonas de risco carregadas: {Total} linhas de {Arquivo}", zonas.Count, caminhoCsvZonas);
        }

        if (criarMotoboysDemo && !await db.Motoboys.AnyAsync(ct))
        {
            await db.Motoboys.AddRangeAsync(MotoboysDemo(relogio.GetUtcNow()), ct);
            await db.SaveChangesAsync(ct);
            log.LogInformation("Motoboys de demonstração criados (dados inventados).");
        }
    }

    /// <summary>Lê o CSV do ML (zona_id, nome, latitude, longitude, faixa_horaria, ..., score, nivel, ...).</summary>
    public static List<ZonaDeRisco> LerZonasCsv(IReadOnlyList<string> linhas)
    {
        if (linhas.Count < 2) throw new RegraDeNegocioException("CSV de zonas vazio.");
        var cab = linhas[0].Split(',').Select((n, i) => (n, i)).ToDictionary(x => x.n.Trim(), x => x.i);
        foreach (var c in new[] { "zona_id", "nome", "latitude", "longitude", "faixa_horaria", "score", "nivel" })
            if (!cab.ContainsKey(c)) throw new RegraDeNegocioException($"Coluna obrigatória ausente no CSV: {c}");

        var inv = CultureInfo.InvariantCulture;
        var zonas = new List<ZonaDeRisco>();
        for (var n = 1; n < linhas.Count; n++)
        {
            if (string.IsNullOrWhiteSpace(linhas[n])) continue;
            var col = linhas[n].Split(','); // os nomes do CSV do ML não têm vírgula
            var zona = new ZonaDeRisco(
                int.Parse(col[cab["zona_id"]], inv), col[cab["nome"]],
                double.Parse(col[cab["latitude"]], inv), double.Parse(col[cab["longitude"]], inv),
                Enum.Parse<FaixaHoraria>(col[cab["faixa_horaria"]]), double.Parse(col[cab["score"]], inv));
            if (zona.Nivel.ToString() != col[cab["nivel"]])
                throw new RegraDeNegocioException($"Linha {n + 1}: nível {col[cab["nivel"]]} não bate com o score {zona.Score}.");
            zonas.Add(zona);
        }
        return zonas;
    }

    private static IEnumerable<Motoboy> MotoboysDemo(DateTimeOffset agora)
    {
        // Pessoas e telefones INVENTADOS (combinado da GS: nada de dado pessoal real).
        var joao = new Motoboy("João Teste da Silva", "(11) 90000-0001", "TST1A01", "ESP32-001", agora);
        joao.AdicionarContato("Maria Teste da Silva", "(11) 90000-1001", "mãe");
        joao.AdicionarContato("Pedro Teste", "(11) 90000-1002", "irmão");
        joao.AtivarDispositivo();

        var ana = new Motoboy("Ana Exemplo Souza", "(11) 90000-0002", "TST2B02", "ESP32-002", agora);
        ana.AdicionarContato("Carlos Exemplo", "(11) 90000-2001", "companheiro");
        ana.AtivarDispositivo();

        var lucas = new Motoboy("Lucas Fictício Lima", "(11) 90000-0003", "TST3C03", "ESP32-003", agora);
        lucas.AdicionarContato("Rita Fictícia", "(11) 90000-3001", "tia");
        // dispositivo do Lucas fica desativado: serve para demonstrar a regra

        return [joao, ana, lucas];
    }
}
