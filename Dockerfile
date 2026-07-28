# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files
COPY *.slnx ./
COPY src/BitRoute.Domain/*.csproj ./src/BitRoute.Domain/
COPY src/BitRoute.Application/*.csproj ./src/BitRoute.Application/
COPY src/BitRoute.Infrastructure/*.csproj ./src/BitRoute.Infrastructure/
COPY src/BitRoute.Api/*.csproj ./src/BitRoute.Api/

# Restore dependencies
RUN dotnet restore src/BitRoute.Api/BitRoute.Api.csproj

# Copy the rest of the source code
COPY src/ ./src/

# Publish
RUN dotnet publish src/BitRoute.Api/BitRoute.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "BitRoute.Api.dll"]
