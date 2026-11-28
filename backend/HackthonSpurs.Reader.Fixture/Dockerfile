FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY backend/NuGet.Config backend/
COPY backend/HackthonSpurs.Resources/ backend/HackthonSpurs.Resources/
COPY backend/HackthonSpurs.Reader.Fixture/ backend/HackthonSpurs.Reader.Fixture/
RUN dotnet publish backend/HackthonSpurs.Reader.Fixture -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
COPY --from=build /publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "HackthonSpurs.Reader.Fixture.dll"]
