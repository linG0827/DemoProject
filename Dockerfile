FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "QrApi.csproj"
RUN dotnet publish "QrApi.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8088
EXPOSE 8088

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "QrApi.dll"]