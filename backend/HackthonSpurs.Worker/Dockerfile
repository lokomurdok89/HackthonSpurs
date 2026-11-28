FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY backend/NuGet.Config backend/
COPY backend/HackthonSpurs.Resources/ backend/HackthonSpurs.Resources/
COPY backend/HackthonSpurs.Worker/ backend/HackthonSpurs.Worker/
COPY backend/HackthonSpurs.Documents/ backend/HackthonSpurs.Documents/
COPY backend/reader-fixture/worker-demo.csv backend/reader-fixture/
RUN dotnet publish backend/HackthonSpurs.Worker -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime
WORKDIR /app
ENTRYPOINT ["dotnet", "HackthonSpurs.Worker.dll"]

# Only the optional SPUR lane needs document layout and PDF rasterization.
FROM runtime AS render-tools
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    libreoffice-writer libreoffice-calc libreoffice-impress poppler-utils fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

FROM render-tools AS document-ocr
COPY --from=build /publish .
USER $APP_UID

FROM runtime AS fixture
COPY --from=build /publish .
USER $APP_UID
