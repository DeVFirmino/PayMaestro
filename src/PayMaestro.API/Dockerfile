FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY PayMaestro.sln ./
COPY src/PayMaestro.Domain/PayMaestro.Domain.csproj src/PayMaestro.Domain/
COPY src/PayMaestro.Application/PayMaestro.Application.csproj src/PayMaestro.Application/
COPY src/PayMaestro.Infrastructure/PayMaestro.Infrastructure.csproj src/PayMaestro.Infrastructure/
COPY src/PayMaestro.API/PayMaestro.API.csproj src/PayMaestro.API/
RUN dotnet restore src/PayMaestro.API/PayMaestro.API.csproj

COPY src/ src/
RUN dotnet publish src/PayMaestro.API/PayMaestro.API.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayMaestro.API.dll"]
