FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Kaynak kod değişiklikleri paket geri yükleme önbelleğini geçersiz kılmasın.
COPY src/VehicleTracking.Domain/VehicleTracking.Domain.csproj src/VehicleTracking.Domain/
COPY src/VehicleTracking.Application/VehicleTracking.Application.csproj src/VehicleTracking.Application/
COPY src/VehicleTracking.Infrastructure/VehicleTracking.Infrastructure.csproj src/VehicleTracking.Infrastructure/
COPY src/VehicleTracking.Api/VehicleTracking.Api.csproj src/VehicleTracking.Api/
RUN dotnet restore src/VehicleTracking.Api/VehicleTracking.Api.csproj

COPY src/ src/
RUN dotnet publish src/VehicleTracking.Api/VehicleTracking.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "VehicleTracking.Api.dll"]
