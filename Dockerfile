# ================================
# STAGE 1: Build
# ================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Carpeta donde se realizará la construcción
WORKDIR /app

# Copiar todos los archivos del proyecto
COPY . .

# Restaurar dependencias (esto es correcto)
RUN dotnet restore

# Publicar en modo Release generando los archivos en /app/out
RUN dotnet publish -c Release -o out


# ================================
# STAGE 2: Runtime
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Carpeta donde correrá la app en el contenedor
WORKDIR /app

# Copiar la publicación del stage de build
COPY --from=build /app/out .

# Exponer puerto 8080
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080


ENTRYPOINT ["dotnet", "backend_proyect.dll"]

