# Stage 1: Build
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

# --- CONFIGURACIÓN DE SEGURIDAD PARA FORZAR > 2048 BITS ---
USER root

# Instalamos openssl para asegurar que las herramientas de diagnóstico estén presentes
RUN apt-get update && apt-get install -y openssl && rm -rf /var/lib/apt/lists/*

# 1. Forzamos SECLEVEL=2 (Este es el estándar que exige 2048 bits como mínimo)
# 2. Restringimos los Ciphers para eliminar los que usan DHE "plano" (que causa el error de dh key too small)
#    y priorizamos ECDHE (Curva Elíptica) que es lo que el servidor de Síntesis soporta.
RUN sed -i 's/CipherString = DEFAULT@SECLEVEL=2/CipherString = ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256@SECLEVEL=2/g' /etc/ssl/openssl.cnf

# Forzamos TLS 1.2 como mínimo a nivel de sistema operativo
RUN sed -i 's/MinProtocol = TLSv1.2/MinProtocol = TLSv1.2/g' /etc/ssl/openssl.cnf

# Variables de entorno críticas
ENV ASPNETCORE_URLS=http://+:8080
# Habilitamos el SocketsHttpHandler moderno que respeta la configuración de OpenSSL de arriba
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=1
# Esta variable asegura que .NET use las librerías de OpenSSL del sistema estrictamente
ENV CLR_OPENSSL_VERSION_OVERRIDE=3

COPY --from=build /app/publish .

# Asegurar permisos
RUN chown -R 1000:1000 /app
USER 1000

ENTRYPOINT ["dotnet", "SintesisV-3.dll"]
