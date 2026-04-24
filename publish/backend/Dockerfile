FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# Copiamos todos los binarios pre-compilados
COPY . .

# Por defecto configuramos para producción y en el puerto 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HSAcademia.API.dll"]
