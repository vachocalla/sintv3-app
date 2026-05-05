# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

USER root

# REEMPLAZO TOTAL DE CONFIGURACIÓN DE OPENSSL
# Esto fuerza el nivel de seguridad 0 (el más permisivo posible) y habilita proveedores legacy
RUN echo 'openssl_conf = openssl_init \n\
\n\
[openssl_init] \n\
providers = provider_sect \n\
ssl_conf = ssl_module \n\
\n\
[provider_sect] \n\
default = default_sect \n\
legacy = legacy_sect \n\
\n\
[default_sect] \n\
activate = 1 \n\
\n\
[legacy_sect] \n\
activate = 1 \n\
\n\
[ssl_module] \n\
system_default = ssl_sect \n\
\n\
[ssl_sect] \n\
CipherString = DEFAULT@SECLEVEL=0 \n\
Options = UnsafeLegacyRenegotiation \n\
MinProtocol = TlsV1 \n\
CipherString = ALL' > /etc/ssl/openssl.cnf

# Forzar a .NET a usar la implementación de sockets estándar
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=1
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

RUN chown -R 1000:1000 /app
USER 1000

ENTRYPOINT ["dotnet", "SintesisV-3.dll"]
