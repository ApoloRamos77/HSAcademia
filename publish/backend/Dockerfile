FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# Copiamos todos los binarios pre-compilados
COPY . .

# Por defecto configuramos para producción y en el puerto 80 (Easypanel default)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "HSAcademia.API.dll"]
