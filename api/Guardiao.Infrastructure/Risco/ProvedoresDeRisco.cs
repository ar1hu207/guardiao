using System.Globalization;
using System.Net.Http.Json;
using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Guardiao.Domain.Interfaces;
using Guardiao.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Risco;

/// <summary>Risco lido do banco da própria API (o zonas_risco.csv do ML é carregado na inicialização).</summary>
public sealed class ZonaRiscoBancoProvider(GuardiaoDbContext db) : IZonaRiscoProvider
{
    public string Origem => "banco-local";

    public async Task<IReadOnlyList<ZonaConsultada>> ConsultarAsync(double latitude, double longitude, int hora,
        double raioKm, int limite, CancellationToken ct = default)
    {
        var faixa = FaixaHorariaRegras.DeHora(hora);

        // 1º o banco filtra pela faixa e por uma caixa ao redor do ponto (usa o índice);
        // 2º a distância exata (haversine) é calculada em memória sobre poucas linhas.
        var dLat = raioKm / 111.0;
        var dLng = raioKm / (111.0 * Math.Max(Math.Cos(latitude * Math.PI / 180), 1e-6));
        var candidatas = await db.ZonasDeRisco.AsNoTracking()
            .Where(z => z.FaixaHoraria == faixa
                        && z.Latitude >= latitude - dLat && z.Latitude <= latitude + dLat
                        && z.Longitude >= longitude - dLng && z.Longitude <= longitude + dLng)
            .ToListAsync(ct);

        return candidatas
            .Select(z => new ZonaConsultada(z, Math.Round(z.DistanciaKm(latitude, longitude), 3)))
            .Where(r => r.DistanciaKm <= raioKm)
            .OrderBy(r => r.DistanciaKm)
            .Take(limite)
            .ToList();
    }
}

/// <summary>
/// Risco consultado no Serviço de Risco (pasta servico-risco/, FastAPI) por REST: SOA na prática.
/// Ativado quando a configuração ServicoRisco:Url está preenchida.
/// </summary>
public sealed class ZonaRiscoServicoHttpProvider(HttpClient http) : IZonaRiscoProvider
{
    public string Origem => "servico-risco";

    private sealed record ZonaRemota(int Id, string Nome, string Nivel, double Score, string FaixaHoraria,
                                     double Latitude, double Longitude, double? DistanciaKm);

    public async Task<IReadOnlyList<ZonaConsultada>> ConsultarAsync(double latitude, double longitude, int hora,
        double raioKm, int limite, CancellationToken ct = default)
    {
        FaixaHorariaRegras.DeHora(hora); // hora inválida vira 400 aqui, sem ir à rede
        var inv = CultureInfo.InvariantCulture;
        var url = $"/api/zonas-risco?lat={latitude.ToString(inv)}&lng={longitude.ToString(inv)}&hora={hora}" +
                  $"&raioKm={raioKm.ToString(inv)}&limite={limite}";
        var zonas = await http.GetFromJsonAsync<List<ZonaRemota>>(url, ct) ?? [];
        return zonas.Select(z => new ZonaConsultada(
                new ZonaDeRisco(z.Id, z.Nome, z.Latitude, z.Longitude, Enum.Parse<FaixaHoraria>(z.FaixaHoraria), z.Score),
                z.DistanciaKm ?? 0))
            .ToList();
    }
}
