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

# Stage 2: Runtime (Imagen solicitada: Alpine 3.22)
FROM mcr.microsoft.com/dotnet/aspnet:8.0.22-alpine3.22 AS final
WORKDIR /app
EXPOSE 8080

# --- CONFIGURACIÓN DE SEGURIDAD PARA INTEGRACIÓN CON SÍNTESIS ---
USER root

# En Alpine usamos apk. Instalamos openssl para diagnóstico y CA-certificates
RUN apk add --no-cache openssl ca-certificates

# Modificación de OpenSSL para cumplir con los requisitos de Síntesis:
# 1. Ajustamos MinProtocol a TLSv1.2.
# 2. Definimos CipherString priorizando ECDHE y eliminando DHE plano para evitar "dh key too small".
# 3. Mantenemos SECLEVEL=2.
RUN sed -i 's/Providers = default_sect/Providers = default_sect\nssl_conf = ssl_module/g' /etc/ssl/openssl.cnf && \
    printf "\n[ssl_module]\nsystem_default = system_default_sect\n" >> /etc/ssl/openssl.cnf && \
    printf "\n[system_default_sect]\nMinProtocol = TLSv1.2\nCipherString = ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256@SECLEVEL=2\n" >> /etc/ssl/openssl.cnf

# Variables de entorno críticas para .NET en entornos Kubernetes/Linux
ENV ASPNETCORE_URLS=http://+:8080
# Forzamos a que .NET use la configuración del stack criptográfico del SO
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=1

COPY --from=build /app/publish .

# Alpine usa por defecto el usuario 'app' (uid 1654) en las imágenes de .NET 8, 
# pero mantendremos tu estándar de UID 1000 si es el que usas en tus Pods de K8s.
RUN adduser -u 1000 -D adminuser || true && \
    chown -R 1000:1000 /app

USER 1000

ENTRYPOINT ["dotnet", "SintesisV-3.dll"]
