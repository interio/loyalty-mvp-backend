FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY src/api/Loyalty.Api.csproj src/api/
COPY tests/Loyalty.Api.Tests/Loyalty.Api.Tests.csproj tests/Loyalty.Api.Tests/
COPY LoyaltyMvp.sln .
RUN dotnet restore LoyaltyMvp.sln

# Copy the rest and publish
COPY . .
RUN dotnet publish src/api/Loyalty.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Loyalty.Api.dll"]
