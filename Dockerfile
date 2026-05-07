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

# Configuración de OpenSSL para soportar llaves DH menores a 2048 bits (SECLEVEL=1)
USER root

# 1. Bajamos el nivel de seguridad de 2 a 1 para permitir llaves de 1024 bits
# 2. Habilitamos el proveedor legacy por si el firewall usa algoritmos muy antiguos
RUN sed -i 's/CipherString = DEFAULT@SECLEVEL=2/CipherString = DEFAULT@SECLEVEL=1/g' /etc/ssl/openssl.cnf && \
    sed -i 's/MinProtocol = TLSv1.2/MinProtocol = TLSv1/g' /etc/ssl/openssl.cnf || true

# Configuración adicional para asegurar que OpenSSL cargue los proveedores correctamente
RUN sed -i 's/Providers = default_sect/Providers = provider_sect/g' /etc/ssl/openssl.cnf && \
    sed -i '/\[provider_sect\]/a legacy = legacy_sect\ndefault = default_sect' /etc/ssl/openssl.cnf && \
    printf "\n[legacy_sect]\nactivate = 1\n[default_sect]\nactivate = 1\n" >> /etc/ssl/openssl.cnf

# Variables de entorno para ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
# Nota: Quitar USESOCKETSHTTPHANDLER=0 suele ser mejor en .NET 8 a menos que tengas un problema específico,
# pero lo mantengo por si tu infraestructura lo requiere.
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

COPY --from=build /app/publish .

# Asegurar permisos para el usuario no root de dotnet
RUN chown -R 1000:1000 /app
USER 1000

ENTRYPOINT ["dotnet", "SintesisV-3.dll"]
