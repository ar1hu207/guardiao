using System.Reflection;
using System.Text.Json.Serialization;
using Guardiao.Api;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGuardiao(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Guardião API",
        Version = "v1",
        Description = "Detecção de queda de motoboys (ESP32), aviso aos contatos de emergência e risco da região " +
                      "pelo modelo de ML. FIAP Global Solution 2026.2."
    });
    foreach (var xml in new[] { Assembly.GetExecutingAssembly().GetName().Name, "Guardiao.Domain" })
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, $"{xml}.xml");
        if (File.Exists(caminho)) o.IncludeXmlComments(caminho, includeControllerXmlComments: true);
    }
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Guardião API v1");
    o.DocumentTitle = "Guardião API";
});

app.UseCors();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Saúde");

await app.InicializarBancoAsync();
await app.RunAsync();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
