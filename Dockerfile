FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CloudWebApp.csproj", "./"]
RUN dotnet restore "CloudWebApp.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "CloudWebApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudWebApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
CMD ASPNETCORE_URLS=http://*:$PORT dotnet CloudWebApp.dll