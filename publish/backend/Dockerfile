FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Copiamos todos los binarios pre-compilados
COPY . .

# Por defecto configuramos para producción y en el puerto 8082
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8082
EXPOSE 8082

ENTRYPOINT ["dotnet", "HSAcademia.API.dll"]
