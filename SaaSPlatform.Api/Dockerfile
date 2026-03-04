# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SaaSPlatform.Api/SaaSPlatform.Api.csproj", "SaaSPlatform.Api/"]
COPY ["SaaSPlatform.Application/SaaSPlatform.Application.csproj", "SaaSPlatform.Application/"]
COPY ["SaaSPlatform.Domain/SaaSPlatform.Domain.csproj", "SaaSPlatform.Domain/"]
COPY ["SaaSPlatform.Infrastructure/SaaSPlatform.Infrastructure.csproj", "SaaSPlatform.Infrastructure/"]
RUN dotnet restore "./SaaSPlatform.Api/./SaaSPlatform.Api.csproj"
COPY . .
WORKDIR "/src/SaaSPlatform.Api"
RUN dotnet build "./SaaSPlatform.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SaaSPlatform.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SaaSPlatform.Api.dll"]
