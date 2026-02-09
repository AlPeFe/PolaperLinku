FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PolaperLinku.Api/PolaperLinku.Api.csproj", "PolaperLinku.Api/"]
RUN dotnet restore "PolaperLinku.Api/PolaperLinku.Api.csproj"
COPY . .
WORKDIR "/src/PolaperLinku.Api"
RUN dotnet build "PolaperLinku.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PolaperLinku.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Instalar dependencias necesarias para Playwright
RUN apt-get update && apt-get install -y \
    wget \
    ca-certificates \
    fonts-liberation \
    libappindicator3-1 \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libcups2 \
    libdbus-1-3 \
    libgdk-pixbuf2.0-0 \
    libnspr4 \
    libnss3 \
    libx11-xcb1 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    xdg-utils \
    libgbm1 \
    libxshmfence1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Instalar Playwright browsers
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN playwright install chromium
RUN playwright install-deps chromium

EXPOSE 5000
ENTRYPOINT ["dotnet", "PolaperLinku.Api.dll"]
