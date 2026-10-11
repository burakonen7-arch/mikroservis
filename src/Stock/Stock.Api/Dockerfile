FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Stock/Stock.Api/Stock.Api.csproj --locked-mode
RUN dotnet publish src/Stock/Stock.Api/Stock.Api.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","Stock.Api.dll"]
