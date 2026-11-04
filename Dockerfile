FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MiniPspPix.sln", "./"]
COPY ["src/MiniPspPix.Domain/MiniPspPix.Domain.csproj", "src/MiniPspPix.Domain/"]
COPY ["src/MiniPspPix.Application/MiniPspPix.Application.csproj", "src/MiniPspPix.Application/"]
COPY ["src/MiniPspPix.Infrastructure/MiniPspPix.Infrastructure.csproj", "src/MiniPspPix.Infrastructure/"]
COPY ["src/MiniPspPix.Api/MiniPspPix.Api.csproj", "src/MiniPspPix.Api/"]

RUN dotnet restore "src/MiniPspPix.Api/MiniPspPix.Api.csproj"

COPY src/ src/
WORKDIR "/src/src/MiniPspPix.Api"
RUN dotnet publish "MiniPspPix.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MiniPspPix.Api.dll"]

