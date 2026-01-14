# See https://aka.ms/customizecontainer

# --------------------
# Base runtime image
# --------------------
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443


# --------------------
# Build + test stage
# --------------------
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# copy csproj files first (better caching)
COPY ["EyeExamParser/EyeExamParser.csproj", "EyeExamParser/"]
COPY ["EyeExamAPI.Tests/EyeExamAPI.Tests.csproj", "EyeExamAPI.Tests/"]

RUN dotnet restore

# copy everything
COPY . .

# build
WORKDIR "/src/EyeExamParser"
RUN dotnet build "EyeExamParser.csproj" -c $BUILD_CONFIGURATION -o /app/build

# --------------------
# TESTS
# --------------------
WORKDIR "/src/EyeExamAPI.Tests"
RUN dotnet test "EyeExamAPI.Tests.csproj" -c $BUILD_CONFIGURATION --no-build


# --------------------
# Publish stage
# --------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/EyeExamParser"
RUN dotnet publish "EyeExamParser.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


# --------------------
# Final runtime image
# --------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EyeExamParser.dll"]
