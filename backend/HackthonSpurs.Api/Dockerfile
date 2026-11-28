FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY backend/NuGet.Config backend/
COPY backend/HackthonSpurs.Api/*.csproj backend/HackthonSpurs.Api/
COPY backend/HackthonSpurs.Documents/*.csproj backend/HackthonSpurs.Documents/
RUN dotnet restore backend/HackthonSpurs.Api
COPY backend/HackthonSpurs.Api/ backend/HackthonSpurs.Api/
COPY backend/HackthonSpurs.Documents/ backend/HackthonSpurs.Documents/
RUN dotnet publish backend/HackthonSpurs.Api -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /publish .
RUN mkdir -p /data/originals && chown -R "${APP_UID}:${APP_UID}" /data/originals
USER $APP_UID
ENTRYPOINT ["dotnet", "HackthonSpurs.Api.dll"]
