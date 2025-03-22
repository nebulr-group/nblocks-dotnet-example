# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory inside container
WORKDIR /app

# Copy project files and restore dependencies
# COPY *.csproj ./
# RUN dotnet restore

# # Copy the rest of the app and build
COPY . ./
# RUN dotnet publish -c Release -o out

# COPY --from=build /app/out .

# Expose port
EXPOSE 5000

# Set environment variable to listen on all network interfaces
ENV ASPNETCORE_URLS=http://+:5000

# Run the app
ENTRYPOINT ["dotnet", "HelloWebApp.dll"]
