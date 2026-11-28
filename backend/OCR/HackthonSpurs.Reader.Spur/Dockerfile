FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY backend/NuGet.Config backend/
COPY backend/Shared/HackthonSpurs.Resources/ backend/Shared/HackthonSpurs.Resources/
COPY backend/OCR/HackthonSpurs.Reader.Spur/ backend/OCR/HackthonSpurs.Reader.Spur/
RUN dotnet publish backend/OCR/HackthonSpurs.Reader.Spur -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
COPY --from=build /publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "HackthonSpurs.Reader.Spur.dll"]
