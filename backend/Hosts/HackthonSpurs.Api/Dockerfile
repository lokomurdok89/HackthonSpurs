FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY backend/NuGet.Config backend/
COPY backend/Shared/HackthonSpurs.Resources/ backend/Shared/HackthonSpurs.Resources/
COPY backend/Core/HackthonSpurs.Domain/*.csproj backend/Core/HackthonSpurs.Domain/
COPY backend/Core/HackthonSpurs.Application/*.csproj backend/Core/HackthonSpurs.Application/
COPY backend/Hosts/HackthonSpurs.Api/*.csproj backend/Hosts/HackthonSpurs.Api/
COPY backend/Adapters/HackthonSpurs.Persistence/*.csproj backend/Adapters/HackthonSpurs.Persistence/
RUN dotnet restore backend/Hosts/HackthonSpurs.Api
COPY backend/Core/HackthonSpurs.Domain/ backend/Core/HackthonSpurs.Domain/
COPY backend/Core/HackthonSpurs.Application/ backend/Core/HackthonSpurs.Application/
COPY backend/Hosts/HackthonSpurs.Api/ backend/Hosts/HackthonSpurs.Api/
COPY backend/Adapters/HackthonSpurs.Persistence/ backend/Adapters/HackthonSpurs.Persistence/
RUN dotnet publish backend/Hosts/HackthonSpurs.Api -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
COPY --from=build /publish .
RUN mkdir -p /data/originals && chown -R "${APP_UID}:${APP_UID}" /data/originals
USER $APP_UID
ENTRYPOINT ["dotnet", "HackthonSpurs.Api.dll"]
