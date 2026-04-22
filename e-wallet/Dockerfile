FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["e-wallet/e-wallet.csproj", "e-wallet/"]
RUN dotnet restore "e-wallet/e-wallet.csproj"

COPY . .
WORKDIR "/src/e-wallet"
RUN dotnet build "e-wallet.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "e-wallet.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "e-wallet.dll"]
