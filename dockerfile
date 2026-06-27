# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy everything and restore
COPY . .
RUN dotnet publish framework-backend/framework-backend.csproj -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Expose port (Railway will override this)
EXPOSE 8080

ENTRYPOINT ["dotnet", "framework-backend.dll"]