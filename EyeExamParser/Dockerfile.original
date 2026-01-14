# ---------- build + test ----------
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS test
WORKDIR /src

COPY . .
RUN dotnet restore
RUN dotnet test EyeExamAPI.Tests/EyeExamAPI.Tests.csproj -c Release --no-restore

# ---------- publish ----------
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore EyeExamParser/EyeExamParser.csproj
RUN dotnet publish EyeExamParser/EyeExamParser.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EyeExamParser.dll"]
