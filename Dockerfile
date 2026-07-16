FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ANU_Admissions.csproj ./
COPY Directory.Build.props ./
COPY NuGet.Config ./
COPY global.json ./
COPY packages.lock.json ./
RUN dotnet restore ANU_Admissions.csproj --locked-mode

COPY . ./
RUN dotnet publish ANU_Admissions.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=true

EXPOSE 8080
COPY --from=build /app/publish ./

USER root
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.aspnet

# .NET 8 Linux images provide this non-root account.
USER app

ENTRYPOINT ["dotnet", "ANU_Admissions.dll"]
