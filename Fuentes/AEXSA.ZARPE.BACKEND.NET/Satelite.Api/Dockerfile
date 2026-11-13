# Contexto de build: la carpeta de la solución (AEXSA.ZARPE.BACKEND.NET).
#   docker build --build-arg IMAGE_TAG=$TAG -f Satelite.Api/Dockerfile -t <registry>/zarpe-backend:$TAG .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "./"]
COPY ["Satelite.Dominio/Satelite.Dominio.csproj", "Satelite.Dominio/"]
COPY ["Satelite.Aplicacion/Satelite.Aplicacion.csproj", "Satelite.Aplicacion/"]
COPY ["Satelite.Infraestructura/Satelite.Infraestructura.csproj", "Satelite.Infraestructura/"]
COPY ["Satelite.Api/Satelite.Api.csproj", "Satelite.Api/"]
RUN dotnet restore "./Satelite.Api/Satelite.Api.csproj"
COPY . .
WORKDIR "/src/Satelite.Api"
RUN dotnet build "./Satelite.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Satelite.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
ARG IMAGE_TAG=local
ENV IMAGE_TAG=$IMAGE_TAG
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Satelite.Api.dll"]
