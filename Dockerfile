# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copiar archivos de solución y proyectos para restaurar paquetes
COPY ["HSAcademia.sln", "./"]
COPY ["src/HSAcademia.Domain/HSAcademia.Domain.csproj", "src/HSAcademia.Domain/"]
COPY ["src/HSAcademia.Application/HSAcademia.Application.csproj", "src/HSAcademia.Application/"]
COPY ["src/HSAcademia.Infrastructure/HSAcademia.Infrastructure.csproj", "src/HSAcademia.Infrastructure/"]
COPY ["src/HSAcademia.API/HSAcademia.API.csproj", "src/HSAcademia.API/"]

RUN dotnet restore "src/HSAcademia.API/HSAcademia.API.csproj"

# Copiar todo el código fuente y compilar
COPY . .
WORKDIR "/app/src/HSAcademia.API"
RUN dotnet publish "HSAcademia.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa de ejecución (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Variables de Entorno por defecto (pueden sobreescribirse al hacer docker run)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "HSAcademia.API.dll"]
