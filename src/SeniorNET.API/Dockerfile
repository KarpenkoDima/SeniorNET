FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY SeniorNET.sln .
COPY src/SeniorNET.Domain/SeniorNET.Domain.csproj src/SeniorNET.Domain/
COPY src/SeniorNET.Application/SeniorNET.Application.csproj src/SeniorNET.Application/
COPY src/SeniorNET.Infrastructure/SeniorNET.Infrastructure.csproj src/SeniorNET.Infrastructure/
COPY src/SeniorNET.API/SeniorNET.API.csproj src/SeniorNET.API/
RUN dotnet restore src/SeniorNET.API/SeniorNET.API.csproj

COPY src/ src/
RUN dotnet publish src/SeniorNET.API/SeniorNET.API.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SeniorNET.API.dll"]
