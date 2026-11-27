FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY NuGet.Config .
COPY src/HackthonSpurs.Api/*.csproj src/HackthonSpurs.Api/
COPY src/HackthonSpurs.Documents/*.csproj src/HackthonSpurs.Documents/
RUN dotnet restore src/HackthonSpurs.Api
COPY src/ src/
RUN dotnet publish src/HackthonSpurs.Api -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
COPY --from=build /publish .
RUN mkdir -p /data/originals && chown -R "${APP_UID}:${APP_UID}" /data/originals
USER $APP_UID
ENTRYPOINT ["dotnet", "HackthonSpurs.Api.dll"]
