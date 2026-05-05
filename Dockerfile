# Stage 1: Build (Asegúrate de que el nombre sea 'build' en minúsculas)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar archivos de proyecto y restaurar
COPY ["src/SintesisV-3/SintesisV-3.csproj", "src/SintesisV-3/"]
RUN dotnet restore "src/SintesisV-3/SintesisV-3.csproj"

# Copiar resto del código y publicar
COPY . .
WORKDIR "/app/src/SintesisV-3"
RUN dotnet publish "SintesisV-3.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

USER root

# REEMPLAZO TOTAL DE CONFIGURACIÓN DE OPENSSL (SECLEVEL 0)
# Usamos un solo comando RUN para evitar problemas de capas y asegurar permisos
RUN printf "openssl_conf = openssl_init\n\
\n\
[openssl_init]\n\
providers = provider_sect\n\
ssl_conf = ssl_module\n\
\n\
[provider_sect]\n\
default = default_sect\n\
legacy = legacy_sect\n\
\n\
[default_sect]\n\
activate = 1\n\
\n\
[legacy_sect]\n\
activate = 1\n\
\n\
[ssl_module]\n\
system_default = ssl_sect\n\
\n\
[ssl_sect]\n\
CipherString = DEFAULT@SECLEVEL=0\n\
Options = UnsafeLegacyRenegotiation\n\
MinProtocol = TlsV1\n" > /etc/ssl/openssl.cnf

# Variables de entorno
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=1

# Copiar desde el stage 'build' (Verifica que el nombre coincida con la línea 2)
COPY --from=build /app/publish .

# Permisos para el usuario dotnet
RUN chown -R 1000:1000 /app
USER 1000

ENTRYPOINT ["dotnet", "SintesisV-3.dll"]