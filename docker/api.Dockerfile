# Zephyrus API. Build from the repository root:
#   docker build -f docker/api.Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached across source changes.
COPY src/Zephyrus.Core/Zephyrus.Core.csproj src/Zephyrus.Core/
COPY src/Zephyrus.Application/Zephyrus.Application.csproj src/Zephyrus.Application/
COPY src/Zephyrus.Infrastructure/Zephyrus.Infrastructure.csproj src/Zephyrus.Infrastructure/
COPY src/Zephyrus.Api/Zephyrus.Api.csproj src/Zephyrus.Api/
RUN dotnet restore src/Zephyrus.Api/Zephyrus.Api.csproj

COPY src/Zephyrus.Core/ src/Zephyrus.Core/
COPY src/Zephyrus.Application/ src/Zephyrus.Application/
COPY src/Zephyrus.Infrastructure/ src/Zephyrus.Infrastructure/
COPY src/Zephyrus.Api/ src/Zephyrus.Api/
COPY prompts/ prompts/
RUN dotnet publish src/Zephyrus.Api/Zephyrus.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# /data holds the encryption key ring and, in demo mode, the demo repositories.
# Mount a volume here so both survive container restarts.
RUN mkdir -p /data/keys /data/demo-repos && chown -R "$APP_UID" /data
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeyPath=/data/keys \
    Demo__RepositoryRoot=/data/demo-repos
EXPOSE 8080
# Healthy once the API answers and can reach the database (/health/ready).
# The image has no curl, so bash speaks HTTP over /dev/tcp.
HEALTHCHECK --interval=10s --timeout=5s --start-period=60s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/ready HTTP/1.0\\r\\n\\r\\n' >&3 && head -n1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "Zephyrus.Api.dll"]
