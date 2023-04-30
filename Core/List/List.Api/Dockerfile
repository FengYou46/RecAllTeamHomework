FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Core/List/List.Api/List.Api.csproj", "Core/List/List.Api/"]
RUN dotnet restore "Core/List/List.Api/List.Api.csproj"
COPY . .
WORKDIR "/src/Core/List/List.Api"
RUN dotnet build "List.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "List.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RecAll.Core.List.Api.dll"]
