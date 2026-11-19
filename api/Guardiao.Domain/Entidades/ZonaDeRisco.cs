namespace Guardiao.Domain.Entidades;

/// <summary>Região de ~2 km numa faixa horária, com o score calculado pelo modelo de ML (ml/zonas_risco.csv).</summary>
public class ZonaDeRisco
{
    private const double RaioTerraKm = 6371.0;

    public int Id { get; private set; }

    /// <summary>Id da célula no CSV do ML (a mesma zona aparece em até 4 faixas).</summary>
    public int ZonaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public FaixaHoraria FaixaHoraria { get; private set; }
    public double Score { get; private set; }
    public NivelRisco Nivel { get; private set; }

    protected ZonaDeRisco() { } // EF Core

    public ZonaDeRisco(int zonaId, string nome, double latitude, double longitude, FaixaHoraria faixa, double score)
    {
        if (zonaId <= 0) throw new RegraDeNegocioException("zonaId deve ser positivo.");
        if (string.IsNullOrWhiteSpace(nome)) throw new RegraDeNegocioException("A zona precisa de um nome.");
        ZonaId = zonaId;
        Nome = nome.Trim();
        Latitude = latitude;
        Longitude = longitude;
        FaixaHoraria = faixa;
        Score = score;
        Nivel = NivelRiscoRegras.DeScore(score); // o nível nunca é escolhido à mão
    }

    /// <summary>Distância em km (haversine) do centro da zona até o ponto.</summary>
    public double DistanciaKm(double latitude, double longitude) => Haversine(Latitude, Longitude, latitude, longitude);

    public static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double g) => g * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lng2 - lng1) / 2), 2);
        return 2 * RaioTerraKm * Math.Asin(Math.Sqrt(a));
    }
}
