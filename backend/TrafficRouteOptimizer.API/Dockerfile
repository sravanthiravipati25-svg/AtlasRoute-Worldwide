FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["TrafficRouteOptimizer.API.csproj", "./"]

RUN dotnet restore "TrafficRouteOptimizer.API.csproj"

COPY . .

RUN dotnet publish "TrafficRouteOptimizer.API.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

# Render provides the PORT environment variable.
ENV ASPNETCORE_URLS=http://+:${PORT}

EXPOSE 10000

ENTRYPOINT ["dotnet", "TrafficRouteOptimizer.API.dll"]
