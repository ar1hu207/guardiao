using Guardiao.Domain;
using Guardiao.Domain.Interfaces;
using Guardiao.Domain.Servicos;
using Guardiao.Infrastructure.Dados;
using Guardiao.Infrastructure.Notificacao;
using Guardiao.Infrastructure.Risco;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api;

/// <summary>Registro de dependências (DI): cada interface do domínio ganha a sua implementação aqui.</summary>
public static class Configuracao
{
    public static IServiceCollection AddGuardiao(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<GuardiaoDbContext>(o =>
            o.UseSqlite(config.GetConnectionString("Guardiao") ?? "Data Source=guardiao.db"));

        services.AddScoped<IMotoboyRepository, MotoboyRepository>();
        services.AddScoped<IOcorrenciaRepository, OcorrenciaRepository>();
        services.AddScoped<IMotoboyService, MotoboyService>();
        services.AddScoped<IOcorrenciaService, OcorrenciaService>();
        services.AddScoped<CargaInicial>();

        // Polimorfismo na configuração: o mesmo IZonaRiscoProvider pode ler o banco local
        // ou chamar o Serviço de Risco (Python/FastAPI) por REST.
        var urlServicoRisco = config["ServicoRisco:Url"];
        if (string.IsNullOrWhiteSpace(urlServicoRisco))
            services.AddScoped<IZonaRiscoProvider, ZonaRiscoBancoProvider>();
        else
            services.AddHttpClient<IZonaRiscoProvider, ZonaRiscoServicoHttpProvider>(c =>
            {
                c.BaseAddress = new Uri(urlServicoRisco);
                c.Timeout = TimeSpan.FromSeconds(5);
            });

        // Todos os INotificador registrados recebem o aviso (o serviço itera sobre IEnumerable<INotificador>).
        var canais = config.GetSection("Notificacao:Canais").Get<string[]>() ?? ["log", "sms"];
        if (canais.Contains("log", StringComparer.OrdinalIgnoreCase))
            services.AddScoped<INotificador, NotificadorLog>();
        if (canais.Contains("sms", StringComparer.OrdinalIgnoreCase))
        {
            services.AddSingleton<IGatewaySms, GatewaySmsSimulado>();
            services.AddScoped<INotificador, NotificadorSms>();
        }

        services.AddExceptionHandler<TratadorDeErros>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>Migra o banco, carrega as zonas do ML e cria os motoboys de demonstração.</summary>
    public static async Task InicializarBancoAsync(this WebApplication app)
    {
        var caminho = app.Configuration["ZonasRisco:CsvPath"] ?? Path.Combine("..", "..", "ml", "zonas_risco.csv");
        if (!Path.IsPathRooted(caminho))
            caminho = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, caminho));
        var criarDemo = app.Configuration.GetValue("Demo:CriarMotoboys", true);

        await using var escopo = app.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CargaInicial>().ExecutarAsync(caminho, criarDemo);
    }
}

/// <summary>Traduz as exceções do domínio em respostas HTTP (ProblemDetails, RFC 9457).</summary>
public sealed class TratadorDeErros(IProblemDetailsService problemDetails, ILogger<TratadorDeErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception erro, CancellationToken ct)
    {
        var (status, titulo) = erro switch
        {
            RegraDeNegocioException => (StatusCodes.Status400BadRequest, "Dado inválido"),
            RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, "Não encontrado"),
            ConflitoException => (StatusCodes.Status409Conflict, "Conflito com o estado atual"),
            HttpRequestException or TaskCanceledException when !ct.IsCancellationRequested
                => (StatusCodes.Status503ServiceUnavailable, "Serviço de Risco indisponível"),
            _ => (0, string.Empty)
        };
        if (status == 0) return false; // erro inesperado: segue para o 500 padrão

        if (status == StatusCodes.Status503ServiceUnavailable)
            log.LogWarning(erro, "Falha ao consultar o Serviço de Risco");

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = erro,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo, Detail = erro.Message }
        });
    }
}
