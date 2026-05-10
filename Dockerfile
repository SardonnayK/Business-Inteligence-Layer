FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Orchestrator.Api/Orchestrator.Api.csproj", "src/Orchestrator.Api/"]
COPY ["src/Orchestrator.Core/Orchestrator.Core.csproj", "src/Orchestrator.Core/"]
COPY ["src/Orchestrator.Engine/Orchestrator.Engine.csproj", "src/Orchestrator.Engine/"]
COPY ["src/Orchestrator.Infrastructure/Orchestrator.Infrastructure.csproj", "src/Orchestrator.Infrastructure/"]
RUN dotnet restore "src/Orchestrator.Api/Orchestrator.Api.csproj"
COPY . .
WORKDIR "/src/src/Orchestrator.Api"
RUN dotnet build "Orchestrator.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Orchestrator.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Orchestrator.Api.dll"]
