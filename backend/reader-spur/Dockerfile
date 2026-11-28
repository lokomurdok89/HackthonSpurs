FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY backend/NuGet.Config backend/
COPY backend/HackthonSpurs.Resources/ backend/HackthonSpurs.Resources/
COPY backend/reader-spur/ backend/reader-spur/
RUN dotnet publish backend/reader-spur -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
COPY --from=build /publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "SpurReader.dll"]
