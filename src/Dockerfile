# Imagen para api y worker. Uso: docker build -f src/Dockerfile --build-arg PROJECT=Districauchos.Api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY global.json Districauchos.sln ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG PROJECT
# Npgsql intenta cargar GSSAPI al conectar; sin esta librería imprime un error inofensivo.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
ENV DOTNET_ENTRY=${PROJECT}.dll
WORKDIR /app
COPY --from=build /app ./
# Fotos bajadas de Drive (volumen fotos_drive): el volumen nuevo copia este dueño, así el Worker puede escribir.
RUN mkdir -p /app/drive/fotos && chown -R app:app /app/drive
USER app
ENTRYPOINT ["sh", "-c", "exec dotnet $DOTNET_ENTRY"]
