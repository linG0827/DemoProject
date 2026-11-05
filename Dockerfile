# 编译阶段
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 安装 Linux 下 System.Drawing 所需的依赖
RUN apt-get update && apt-get install -y libgdiplus libc6-dev

COPY . .
RUN dotnet restore "QrApi.csproj"
RUN dotnet publish "QrApi.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 运行环境同样需要安装 libgdiplus
RUN apt-get update && apt-get install -y libgdiplus && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "QrApi.dll"]