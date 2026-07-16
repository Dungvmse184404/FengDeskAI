# =====================================================================
# FengDeskAI — Dockerfile (multi-stage) cho .NET 8 WebAPI
# Build context = thư mục gốc repo (chứa FengDeskAI.slnx + src/)
# Cấu hình đọc từ appsettings.json (được copy theo khi publish).
# =====================================================================

# ---------- Stage 1: restore + build + publish ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file project trước để tận dụng cache layer khi restore.
COPY src/FengDeskAI.Domain/FengDeskAI.Domain.csproj           src/FengDeskAI.Domain/
COPY src/FengDeskAI.Contracts/FengDeskAI.Contracts.csproj     src/FengDeskAI.Contracts/
COPY src/FengDeskAI.Application/FengDeskAI.Application.csproj  src/FengDeskAI.Application/
COPY src/FengDeskAI.Infrastructure/FengDeskAI.Infrastructure.csproj src/FengDeskAI.Infrastructure/
COPY src/FengDeskAI.WebAPI/FengDeskAI.WebAPI.csproj           src/FengDeskAI.WebAPI/

RUN dotnet restore src/FengDeskAI.WebAPI/FengDeskAI.WebAPI.csproj

# Copy toàn bộ source rồi publish (appsettings.json đi kèm trong output).
COPY . .
RUN dotnet publish src/FengDeskAI.WebAPI/FengDeskAI.WebAPI.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Production environment; Kestrel lắng nghe cổng 8080 trong container.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

# Chạy bằng user không phải root cho an toàn.
USER $APP_UID

COPY --from=build /app/publish .
# Seed data JSON (đọc lúc chạy seeder) — nằm ngoài code app, xem seed-data/README.md.
COPY --from=build /src/seed-data ./seed-data

EXPOSE 8080
ENTRYPOINT ["dotnet", "FengDeskAI.WebAPI.dll"]
