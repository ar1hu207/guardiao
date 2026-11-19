using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Guardiao.Tests.Integracao;

/// <summary>Sobe a API de verdade (rotas, DI, EF Core + SQLite, migração, carga do CSV) num banco temporário.</summary>
public sealed class GuardiaoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _banco = Path.Combine(Path.GetTempPath(), $"guardiao-teste-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        builder.UseSetting("ConnectionStrings:Guardiao", $"Data Source={_banco}");
        builder.UseSetting("ServicoRisco:Url", "");
        builder.UseSetting("Notificacao:Canais:0", "sms");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_banco); } catch (IOException) { /* o arquivo é temporário */ }
    }
}

public class ApiTests(GuardiaoApiFactory fabrica) : IClassFixture<GuardiaoApiFactory>
{
    private readonly HttpClient _http = fabrica.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>());

    private Task<HttpResponseMessage> PostOcorrencia(object corpo) => _http.PostAsJsonAsync("/api/ocorrencias", corpo);

    [Fact]
    public async Task Queda_do_esp32_cria_ocorrencia_avisa_contatos_e_aparece_no_historico()
    {
        var r = await PostOcorrencia(new { dispositivoId = "ESP32-001", tipo = "QUEDA", latitude = -23.5905, longitude = -46.7889, intensidadeG = 4.2 });

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var corpo = await Json(r);
        var id = corpo.GetProperty("ocorrencia").GetProperty("id").GetInt32();
        Assert.Equal($"/api/ocorrencias/{id}", r.Headers.Location!.AbsolutePath);
        Assert.Equal("ABERTA", corpo.GetProperty("ocorrencia").GetProperty("status").GetString());
        Assert.Equal(2, corpo.GetProperty("contatosAvisados").GetArrayLength());
        Assert.Equal(78, corpo.GetProperty("riscoNoLocal").GetProperty("id").GetInt32());

        var get = await _http.GetFromJsonAsync<JsonElement>($"/api/ocorrencias/{id}");
        Assert.Equal(2, get.GetProperty("contatosNotificados").GetInt32());

        var historico = await _http.GetFromJsonAsync<JsonElement[]>("/api/motoboys/1/ocorrencias");
        Assert.Contains(historico!, o => o.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Cancelar_so_funciona_uma_vez()
    {
        var criada = await Json(await PostOcorrencia(new { dispositivoId = "ESP32-002", tipo = "SOS_MANUAL", latitude = -23.56, longitude = -46.65 }));
        var id = criada.GetProperty("ocorrencia").GetProperty("id").GetInt32();

        var primeira = await _http.PatchAsync($"/api/ocorrencias/{id}/cancelar", null);
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal("CANCELADA", (await Json(primeira)).GetProperty("status").GetString());

        var segunda = await _http.PatchAsync($"/api/ocorrencias/{id}/cancelar", null);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal("application/problem+json", segunda.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Dispositivo_desconhecido_responde_404()
    {
        var r = await PostOcorrencia(new { dispositivoId = "ESP32-999", tipo = "QUEDA", latitude = -23.5, longitude = -46.6, intensidadeG = 3.0 });
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("ESP32-999", (await Json(r)).GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("""{"dispositivoId":"ESP32-001","tipo":"QUEDA","latitude":-23.5,"longitude":-46.6}""")]          // sem intensidadeG
    [InlineData("""{"dispositivoId":"ESP32-001","tipo":"PANICO","latitude":-23.5,"longitude":-46.6}""")]         // tipo fora do enum
    [InlineData("""{"dispositivoId":"ESP32-001","tipo":"SOS_MANUAL","latitude":-123.5,"longitude":-46.6}""")]    // fora do globo
    [InlineData("""{"tipo":"SOS_MANUAL","latitude":-23.5,"longitude":-46.6}""")]                               // sem dispositivo
    public async Task Corpo_invalido_responde_400(string json)
    {
        var r = await _http.PostAsync("/api/ocorrencias", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Queda_com_dispositivo_desativado_responde_409()
    {
        var r = await PostOcorrencia(new { dispositivoId = "ESP32-003", tipo = "QUEDA", latitude = -23.5, longitude = -46.6, intensidadeG = 3.0 });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Zonas_de_risco_seguem_o_contrato()
    {
        var r = await _http.GetAsync("/api/zonas-risco?lat=-23.5905&lng=-46.7889&hora=2");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("banco-local", r.Headers.GetValues("X-Origem-Risco").Single());

        var zonas = (await Json(r)).EnumerateArray().ToList();
        Assert.NotEmpty(zonas);
        var primeira = zonas[0];
        Assert.Equal(78, primeira.GetProperty("id").GetInt32());
        Assert.Equal("ALTO", primeira.GetProperty("nivel").GetString());
        Assert.Equal("MADRUGADA", primeira.GetProperty("faixaHoraria").GetString());
        foreach (var campo in new[] { "id", "nome", "nivel", "score" })
            Assert.True(primeira.TryGetProperty(campo, out _), campo);
        var distancias = zonas.Select(z => z.GetProperty("distanciaKm").GetDouble()).ToList();
        Assert.Equal(distancias.Order(), distancias);
    }

    [Theory]
    [InlineData("/api/zonas-risco?lat=-23.5&lng=-46.6&hora=24")]
    [InlineData("/api/zonas-risco?lng=-46.6&hora=2")]
    [InlineData("/api/zonas-risco?lat=-23.5&lng=-46.6&hora=2&raioKm=500")]
    public async Task Zonas_com_parametro_invalido_respondem_400(string url) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync(url)).StatusCode);

    [Fact]
    public async Task Crud_de_motoboy_e_contatos()
    {
        var criado = await _http.PostAsJsonAsync("/api/motoboys",
            new { nome = "Bruno Teste Rocha", telefone = "(11) 90000-0009", placa = "TST9Z99", dispositivoId = "esp32-009" });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var id = (await Json(criado)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.Conflict, (await _http.PatchAsync($"/api/motoboys/{id}/dispositivo/ativar", null)).StatusCode);

        var contato = await _http.PostAsJsonAsync($"/api/motoboys/{id}/contatos",
            new { nome = "Joana Teste Rocha", telefone = "(11) 90000-9001", parentesco = "esposa" });
        Assert.Equal(HttpStatusCode.Created, contato.StatusCode);
        var contatoId = (await Json(contato)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await _http.PatchAsync($"/api/motoboys/{id}/dispositivo/ativar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _http.DeleteAsync($"/api/motoboys/{id}/contatos/{contatoId}")).StatusCode);

        var put = await _http.PutAsJsonAsync($"/api/motoboys/{id}",
            new { nome = "Bruno Teste Rocha Jr", telefone = "(11) 90000-0009", placa = "TST9Z98", dispositivoId = "ESP32-009" });
        Assert.Equal("Bruno Teste Rocha Jr", (await Json(put)).GetProperty("nome").GetString());

        var contatos = await _http.GetFromJsonAsync<JsonElement[]>($"/api/motoboys/{id}/contatos");
        Assert.Single(contatos!);

        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/motoboys/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/motoboys/{id}")).StatusCode);
    }

    [Fact]
    public async Task Placa_repetida_responde_409()
    {
        var r = await _http.PostAsJsonAsync("/api/motoboys",
            new { nome = "Outro Teste", telefone = "(11) 90000-0077", placa = "TST1A01", dispositivoId = "ESP32-077" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Swagger_documenta_as_rotas_do_contrato()
    {
        var doc = await _http.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var rotas = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        foreach (var rota in new[] { "/api/ocorrencias", "/api/ocorrencias/{id}/cancelar", "/api/motoboys/{id}/ocorrencias",
                                     "/api/zonas-risco", "/api/motoboys", "/api/motoboys/{id}", "/api/motoboys/{id}/contatos" })
            Assert.Contains(rota, rotas);
    }
}
