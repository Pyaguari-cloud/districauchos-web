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
ENV DOTNET_ENTRY=${PROJECT}.dll
WORKDIR /app
COPY --from=build /app ./
USER app
ENTRYPOINT ["sh", "-c", "exec dotnet $DOTNET_ENTRY"]
