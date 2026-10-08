FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ src/Shared/
COPY src/Services/KubeShop.Catalog.Api/ src/Services/KubeShop.Catalog.Api/
RUN dotnet publish src/Services/KubeShop.Catalog.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "KubeShop.Catalog.Api.dll"]
