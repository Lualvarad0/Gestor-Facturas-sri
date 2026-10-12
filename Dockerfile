# Imagen de producción: compila el frontend Angular y la API .NET, y sirve ambos desde ASP.NET Core.
# docker build -t nudo .

# 1. Frontend (Angular)
FROM node:22-alpine AS web
WORKDIR /src/Web
COPY Web/package.json Web/package-lock.json ./
RUN npm ci
COPY Web/ ./
RUN npx ng build --configuration production

# 2. API (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS back
WORKDIR /src
COPY Back/Back.csproj Back/
RUN dotnet restore Back/Back.csproj
COPY Back/ Back/
RUN dotnet publish Back/Back.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3. Ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=back /app/publish ./
COPY --from=web /src/Web/dist/web/browser ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Back.dll"]
