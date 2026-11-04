FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MiniPspPix.sln", "./"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/MiniPspPix.API/MiniPspPix.API.csproj", "src/MiniPspPix.API/"]

RUN dotnet restore "src/MiniPspPix.API/MiniPspPix.API.csproj"

COPY src/ src/
WORKDIR "/src/src/MiniPspPix.API"
RUN dotnet publish "MiniPspPix.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MiniPspPix.API.dll"]
