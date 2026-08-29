# syntax=docker/dockerfile:1
# Build stage: restore + publish the web project (pulls in Domain and Infrastructure).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish FeedCraft.Web.csproj -c Release -o /app/publish

# Runtime stage: ASP.NET Core runtime only, no SDK.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Render detects the open port; .NET listens on 8080 via this env var.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Migrations run inside Program.cs on startup, so the SQLite database
# (feedcraft.db) is created and seeded on first boot — no manual step.
ENTRYPOINT ["dotnet", "FeedCraft.Web.dll"]
