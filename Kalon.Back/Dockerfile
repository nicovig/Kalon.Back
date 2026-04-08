FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Kalon.Back.sln .
COPY Kalon.Back/Kalon.Back.csproj Kalon.Back/
COPY Kalon.Back.Tests/Kalon.Back.Tests.csproj Kalon.Back.Tests/
RUN dotnet restore Kalon.Back/Kalon.Back.csproj

COPY . .
RUN dotnet publish Kalon.Back/Kalon.Back.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kalon.Back.dll"]