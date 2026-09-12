# ---------- Etapa 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo el .csproj primero (aprovecha el cache de Docker)
COPY ErpModa.Api/ErpModa.Api.csproj ErpModa.Api/
RUN dotnet restore ErpModa.Api/ErpModa.Api.csproj

# Copiar el resto del código y publicar
COPY . .
RUN dotnet publish ErpModa.Api/ErpModa.Api.csproj -c Release -o /app/publish

# ---------- Etapa 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ErpModa.Api.dll"]