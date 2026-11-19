using System.Net;
using System.Text;
using Guardiao.Domain;
using Guardiao.Infrastructure.Dados;
using Guardiao.Infrastructure.Risco;

namespace Guardiao.Tests.Infra;

public class CargaCsvTests
{
    private static string CaminhoCsvDoMl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ml", "zonas_risco.csv")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new FileNotFoundException("ml/zonas_risco.csv"), "ml", "zonas_risco.csv");
    }

    [Fact]
    public void Le_o_csv_gerado_pelo_ml()
    {
        var zonas = CargaInicial.LerZonasCsv(File.ReadAllLines(CaminhoCsvDoMl()));

        Assert.Equal(794, zonas.Count);
        Assert.Equal(222, zonas.Select(z => z.ZonaId).Distinct().Count());
        var sp270 = zonas.Single(z => z.ZonaId == 78 && z.FaixaHoraria == FaixaHoraria.MADRUGADA);
        Assert.Equal(NivelRisco.ALTO, sp270.Nivel);
        Assert.All(zonas, z => Assert.InRange(z.Score, 0, 1));
    }

    [Fact]
    public void Coluna_obrigatoria_faltando_e_erro()
    {
        string[] csv = ["zona_id,nome,latitude,longitude,faixa_horaria,score", "1,X,-23.5,-46.6,NOITE,0.5"];
        var erro = Assert.Throws<RegraDeNegocioException>(() => CargaInicial.LerZonasCsv(csv));
        Assert.Contains("nivel", erro.Message);
    }

    [Fact]
    public void Nivel_que_nao_bate_com_o_score_e_erro()
    {
        string[] csv = ["zona_id,nome,latitude,longitude,faixa_horaria,score,nivel", "1,X,-23.5,-46.6,NOITE,0.95,BAIXO"];
        Assert.Throws<RegraDeNegocioException>(() => CargaInicial.LerZonasCsv(csv));
    }
}

/// <summary>SOA: a API C# consome o Serviço de Risco (Python) por HTTP. Aqui o servidor é simulado.</summary>
public class ServicoRiscoHttpTests
{
    private sealed class ServidorFalso(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        public Uri? UltimaUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            UltimaUrl = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            });
        }
    }

    private const string RespostaPython = """
        [{"id":78,"nome":"SP-270 e entorno","nivel":"ALTO","score":1.0,"faixaHoraria":"MADRUGADA",
          "latitude":-23.59,"longitude":-46.79,"distanciaKm":0.125,"riscoRelativo":2.9}]
        """;

    [Fact]
    public async Task Converte_a_resposta_do_servico_python()
    {
        var servidor = new ServidorFalso(HttpStatusCode.OK, RespostaPython);
        var provedor = new ZonaRiscoServicoHttpProvider(new HttpClient(servidor) { BaseAddress = new Uri("http://risco:8000") });

        var r = await provedor.ConsultarAsync(-23.5905, -46.7889, 2, 2, 5);

        Assert.Equal("/api/zonas-risco?lat=-23.5905&lng=-46.7889&hora=2&raioKm=2&limite=5", servidor.UltimaUrl!.PathAndQuery);
        var zona = Assert.Single(r);
        Assert.Equal(78, zona.Zona.ZonaId);
        Assert.Equal(NivelRisco.ALTO, zona.Zona.Nivel);
        Assert.Equal(0.125, zona.DistanciaKm);
    }

    [Fact]
    public async Task Hora_invalida_nem_chega_a_ir_para_a_rede()
    {
        var servidor = new ServidorFalso(HttpStatusCode.OK, "[]");
        var provedor = new ZonaRiscoServicoHttpProvider(new HttpClient(servidor) { BaseAddress = new Uri("http://risco:8000") });

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => provedor.ConsultarAsync(-23.5, -46.6, 30, 2, 5));
        Assert.Null(servidor.UltimaUrl);
    }

    [Fact]
    public async Task Servico_fora_do_ar_vira_HttpRequestException()
    {
        var servidor = new ServidorFalso(HttpStatusCode.ServiceUnavailable, "{}");
        var provedor = new ZonaRiscoServicoHttpProvider(new HttpClient(servidor) { BaseAddress = new Uri("http://risco:8000") });

        await Assert.ThrowsAsync<HttpRequestException>(() => provedor.ConsultarAsync(-23.5, -46.6, 2, 2, 5));
    }
}
