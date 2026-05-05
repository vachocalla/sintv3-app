using Microsoft.AspNetCore.Mvc;
using SintesisV_3;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);


builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});

app.MapPost("/login", async ([FromBody] MyLoginRequest loginRequest, IConfiguration config, ILogger<Program> logger) =>
{
    logger.LogInformation("Iniciando proceso de login para SintesisV-3 a las {Time}", DateTime.Now);

    var handler = new HttpClientHandler
    {
        SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        // Validación de certificado habilitada por defecto al no existir el callback
    };

    using HttpClient client = new HttpClient(handler);
    string url = "https://web.sintesis.com.bo/SintesisIntegradoRest/integrado/integrado/iniciarSesion";

    try
    {
        logger.LogInformation("Enviando petición POST a Síntesis para el usuario: {User}", loginRequest.usuario);

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "SintesisV-3-Proxy");

        var response = await client.PostAsync(url, content);
        var result = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Respuesta exitosa recibida de Síntesis. Código: {StatusCode}", response.StatusCode);
        }
        else
        {
            logger.LogWarning("Síntesis respondió con error. Código: {StatusCode}. Body: {Body}", response.StatusCode, result);
        }

        return Results.Content(result, "application/json", Encoding.UTF8);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error crítico al intentar conectar con Síntesis");
        return Results.Problem($"Error: {ex.Message}");
    }
})
.WithName("IniciarSesionSintesis")
.Accepts<MyLoginRequest>("application/json")
.WithOpenApi(operation =>
{
    operation.Summary = "Inicio de Sesión Síntesis";
    operation.Description = "Envía credenciales para obtener acceso.";
    return operation;
});

app.Run();