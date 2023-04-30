FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Infrastructure/ServiceStatus/ServiceStatus.csproj", "Infrastructure/ServiceStatus/"]
RUN dotnet restore "Infrastructure/ServiceStatus/ServiceStatus.csproj"
COPY . .
WORKDIR "/src/Infrastructure/ServiceStatus"
RUN dotnet build "ServiceStatus.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ServiceStatus.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RecAll.Infractructure.ServiceStatus.dll"]
