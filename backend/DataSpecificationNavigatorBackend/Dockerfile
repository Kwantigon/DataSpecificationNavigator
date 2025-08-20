FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Restore dependencies.
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the application code
COPY . ./

# Publish the application for a release build.
RUN dotnet publish -c Release -o /app/publish

# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

# Copy the published application from the build stage.
COPY --from=build /app/publish .

EXPOSE 8080

# Set the entry point for the application
ENTRYPOINT ["dotnet", "DataSpecificationNavigatorBackend.dll"]
