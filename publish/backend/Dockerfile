FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Copiamos todos los binarios pre-compilados
COPY . .

# Carpeta de comprobantes de pago: montar como VOLUMEN persistente en Easypanel
# (Montajes → Volumen → ruta /app/wwwroot/uploads); si no, se pierden en cada despliegue.
RUN mkdir -p /app/wwwroot/uploads
VOLUME ["/app/wwwroot/uploads"]

# Producción en el puerto 8082. Los secretos (BD, JWT) llegan por variables de entorno.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8082
EXPOSE 8082

ENTRYPOINT ["dotnet", "HSAcademia.API.dll"]
