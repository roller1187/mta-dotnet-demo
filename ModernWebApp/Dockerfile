# Build stage - using Red Hat Universal Base Image 8 with .NET 8 SDK
FROM registry.access.redhat.com/ubi8/dotnet-80:latest AS build
WORKDIR /opt/app-root/src

# Copy csproj and restore dependencies
COPY --chown=1001:0 ModernWebApp.csproj .
RUN dotnet restore

# Copy everything else and build
COPY --chown=1001:0 . .
RUN dotnet publish -c Release -o /opt/app-root/publish

# Runtime stage - using Red Hat Universal Base Image 8 with .NET 8 Runtime
FROM registry.access.redhat.com/ubi8/dotnet-80-runtime:latest AS runtime
WORKDIR /opt/app-root

# UBI images already run as user 1001 (default non-root user)
# No need to create a new user - OpenShift compatible out of the box
COPY --chown=1001:0 --from=build /opt/app-root/publish .

# Expose port 8080 (OpenShift default)
EXPOSE 8080

# Set environment variable for ASP.NET Core to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ModernWebApp.dll"]
