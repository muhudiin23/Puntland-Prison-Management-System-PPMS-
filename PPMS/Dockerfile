FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PPMS.csproj", "."]
RUN dotnet restore "PPMS.csproj"
COPY . .
RUN dotnet build "PPMS.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PPMS.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/wwwroot/uploads/profiles /app/wwwroot/uploads/prisoners /app/wwwroot/uploads/wanted
ENTRYPOINT ["dotnet", "PPMS.dll"]
