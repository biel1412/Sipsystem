# Build: docker build -t sip-net .   (rodar na raiz do repositório)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Sip.Api/Sip.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Sip.Api.dll"]
