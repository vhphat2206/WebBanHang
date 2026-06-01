# Multi-stage build: backend .NET + frontend static files
# Build context = repo root để access cả backend/ lẫn frontend/

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Cache restore layer
COPY backend/backend.csproj ./backend/
RUN dotnet restore backend/backend.csproj

# Build + publish backend
COPY backend/ ./backend/
RUN dotnet publish backend/backend.csproj -c Release -o /app/backend

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy backend build output
COPY --from=build /app/backend ./backend/

# Copy frontend (Program.cs sẽ serve từ ../frontend)
COPY frontend/ ./frontend/

# Run từ backend folder để path "../frontend" trỏ đúng
WORKDIR /app/backend

# Render/PaaS thường set PORT env; default fallback 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "backend.dll"]
