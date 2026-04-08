FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Kalon.Back.sln ../
COPY Kalon.Back.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish Kalon.Back.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kalon.Back.dll"]