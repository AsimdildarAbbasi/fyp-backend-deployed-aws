# BIIT Office Boy Management System (OBMS) — Full Cloud Deployment & DevOps Log

This repository houses the complete backend architecture, docker configurations, and cloud infrastructure logs for migrating the **BIIT Office Boy Management System (OBMS)** out of a standard `localhost` environment and into a decoupled, serverless micro-service architecture on **Amazon Web Services (AWS)**.

Instead of treating this as a simple university submission, the system was architected, deployed, debugged, and managed like a real-world enterprise cloud product.

---

## 🏗️ 1. Complete Cloud Architecture Overview

The system is decoupled into isolated, specialized infrastructure layers on the cloud to maximize uptime and performance:

* **Frontend UI Layer (AWS Amplify):** Hosts our production Next.js frontend client directly from Git branches, delivering pages fast via global edge CDNs.
* **Serverless Compute Layer (AWS ECS Fargate):** Orchestrates and runs our backend `.NET Core API` inside secure, serverless Linux containers. No EC2 management required.
* **Managed Database Layer (AWS RDS):** Runs a fully managed, high-performance Microsoft SQL Server database engine.
* **Image Registries (AWS ECR):** Stores and version-controls our immutable, frozen Docker deployment images.

---

## 🐳 2. Containerization Engine

The application logic is packaged into a minimal, production-ready Linux runtime layer to guarantee that the application behaves exactly the same in development as it does on AWS.

### Production `Dockerfile`
```dockerfile
FROM [mcr.microsoft.com/dotnet/aspnet:8.0](https://mcr.microsoft.com/dotnet/aspnet:8.0) AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM [mcr.microsoft.com/dotnet/sdk:8.0](https://mcr.microsoft.com/dotnet/sdk:8.0) AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OBManagementAPI.csproj", "."]
RUN dotnet restore "./OBManagementAPI.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./OBManagementAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OBManagementAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OBManagementAPI.dll"]
