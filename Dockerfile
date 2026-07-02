FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY API/API.csproj API/
RUN dotnet restore API/API.csproj
COPY API/ API/
RUN dotnet publish API/API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Volume persistant des templates .mrt
VOLUME /data/pdf-designer
EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
