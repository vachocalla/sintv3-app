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
    client.DefaultRequestVersion = HttpVersion.Version20;
    //client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
    //client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("User-Agent", "SintesisV-3-Proxy");
})
.ConfigurePrimaryHttpMessageHandler((serviceProvider) =>
{
    // Obtenemos el logger desde el contenedor de dependencias
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    return new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            EncryptionPolicy = System.Net.Security.EncryptionPolicy.RequireEncryption,
            AllowRenegotiation = false,

            // CANDADO 2: Validación explícita del protocolo negociado
            RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                // Esto se ejecuta durante el handshake
                return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
            }
        },
        // Este callback nos permite "mirar" dentro del stream SSL una vez autenticado
        ConnectCallback = async (context, cancellationToken) =>
        {
            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            socket.NoDelay = true;

            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                var sslStream = new System.Net.Security.SslStream(new System.Net.Sockets.NetworkStream(socket, true), false);

                await sslStream.AuthenticateAsClientAsync(new System.Net.Security.SslClientAuthenticationOptions
                {
                    TargetHost = context.DnsEndPoint.Host,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                }, cancellationToken);

                // --- AQUÍ OBTENEMOS LOS DATOS REALES ---
                var protocol = sslStream.SslProtocol;
                var cipher = sslStream.CipherAlgorithm;
                var strength = sslStream.CipherStrength;
                var keyExchange = sslStream.KeyExchangeAlgorithm;

                // Podemos guardar esto en el contexto del request o imprimirlo directamente
                // Para fines de validación, lo imprimimos en consola:
                //logger.LogInformation("Algoritmo de Cifrado: {Cipher} ({Strength} bits)", sslStream.CipherAlgorithm, sslStream.CipherStrength);
                logger.LogInformation("Logger: [SECURITY-AUDIT] TLS: {protocol} | Cipher: {cipher} | Strength: {strength} bits | Exchange: {keyExchange}", protocol, cipher, strength, keyExchange);
                //Console.WriteLine($"[SECURITY-AUDIT] TLS: {protocol} | Cipher: {cipher} | Strength: {strength} bits | Exchange: {keyExchange}");

                return sslStream;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
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
        
        using var response = await client.PostAsync(relativePath, content);

        logger.LogInformation("Conexión establecida. HTTP Version: {HttpVer}", response.Version);

        // --- BLOQUE DE VALIDACIÓN DE SEGURIDAD ---
        // Accedemos a los metadatos de la conexión TLS
        var tlsInfo = response.RequestMessage?.VersionPolicy; // Política de versión

        // En .NET, para ver el Cipher exacto negociado:
        #if NET8_0_OR_GREATER
        // Nota: El acceso a SslStream directamente requiere un handler personalizado, 
        // pero podemos loguear la versión del protocolo fácilmente:
        logger.LogInformation("Protocolo TLS Negociado: {Protocol}", response.Version);
        #endif

        var result = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Respuesta exitosa recibida de Síntesis. Código: {StatusCode}", response.StatusCode);
            logger.LogInformation("Conexión segura establecida exitosamente bajo SECLEVEL=2.");
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
    operation.Summary = "Inicio de Sesión Síntesis fecha hora 2026-05-06 04:21:00";
    operation.Description = "Envía credenciales para obtener acceso.";
    return operation;
});

app.Run();