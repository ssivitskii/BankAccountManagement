FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source
COPY Directory.Build.props Directory.Packages.props ./
COPY .editorconfig ./
COPY src/Banking.Domain/Banking.Domain.csproj src/Banking.Domain/
COPY src/Banking.Application/Banking.Application.csproj src/Banking.Application/
COPY src/Banking.Infrastructure/Banking.Infrastructure.csproj src/Banking.Infrastructure/
COPY src/Banking.Api/Banking.Api.csproj src/Banking.Api/
RUN dotnet restore src/Banking.Api/Banking.Api.csproj
COPY . ./
RUN dotnet publish src/Banking.Api/Banking.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Banking.Api.dll"]
