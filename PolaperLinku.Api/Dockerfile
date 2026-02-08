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
COPY --from=publish /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "PolaperLinku.Api.dll"]
