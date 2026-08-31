FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/PayMaestro.FakeProvider/PayMaestro.FakeProvider.csproj src/PayMaestro.FakeProvider/
RUN dotnet restore src/PayMaestro.FakeProvider/PayMaestro.FakeProvider.csproj

COPY src/PayMaestro.FakeProvider/ src/PayMaestro.FakeProvider/
RUN dotnet publish src/PayMaestro.FakeProvider/PayMaestro.FakeProvider.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayMaestro.FakeProvider.dll"]
