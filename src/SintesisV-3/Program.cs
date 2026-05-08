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

// Registro del HttpClient con la configuración de SocketsHttpHandler solicitada
builder.Services.AddHttpClient("SintesisClient", client =>
{
    client.BaseAddress = new Uri("https://web.sintesis.com.bo"); // Ajustado a la URL del código original
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("User-Agent", "SintesisV-3-Proxy");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            EncryptionPolicy = System.Net.Security.EncryptionPolicy.RequireEncryption,
            AllowRenegotiation = false
        }
    };
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});

app.MapPost("/login", async ([FromBody] MyLoginRequest loginRequest, IConfiguration config, ILogger<Program> logger, IHttpClientFactory httpClientFactory) =>
{
    logger.LogInformation("VicV1.0.0.1 06052026 0959 Iniciando proceso de login para SintesisV-3 a las {Time}", DateTime.Now);

    // Se obtiene el cliente configurado desde la factoría
    using HttpClient client = httpClientFactory.CreateClient("SintesisClient");
    string relativePath = "/SintesisIntegradoRest/integrado/integrado/iniciarSesion";

    try
    {
        logger.LogInformation("Enviando petición POST a Síntesis para el usuario: {User}", loginRequest.usuario);

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await client.PostAsync(relativePath, content);
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
    operation.Summary = "Inicio de Sesión Síntesis fecha hora 2026-05-06 03:57:00";
    operation.Description = "Envía credenciales para obtener acceso.";
    return operation;
});

app.Run();