# 编译阶段
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "QrApi.csproj"
RUN dotnet publish "QrApi.csproj" -c Release -o /app/publish --no-restore

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SkiaSharp 在 Linux 上需要字体库
RUN apt-get update && apt-get install -y libfontconfig1 && rm -rf /var/lib/apt/lists/*

# 容器内部监听 8011
ENV ASPNETCORE_URLS=http://+:8011
EXPOSE 8011

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "QrApi.dll"]