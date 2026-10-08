FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ src/Shared/
COPY src/Services/KubeShop.Orders.Api/ src/Services/KubeShop.Orders.Api/
RUN dotnet publish src/Services/KubeShop.Orders.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "KubeShop.Orders.Api.dll"]
