FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ src/Shared/
COPY src/Services/KubeShop.Notifications.Worker/ src/Services/KubeShop.Notifications.Worker/
RUN dotnet publish src/Services/KubeShop.Notifications.Worker -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "KubeShop.Notifications.Worker.dll"]
