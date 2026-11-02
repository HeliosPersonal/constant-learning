FROM node:22-bookworm-slim AS frontend
WORKDIR /src/ConstantLearning.MiniApp
COPY ConstantLearning.MiniApp/package*.json ./
RUN npm ci
COPY ConstantLearning.MiniApp/ ./
COPY docs/features/personal-trainer-mvp/design/theme-tokens.json /src/docs/features/personal-trainer-mvp/design/theme-tokens.json
RUN npm run generate:tokens && npx vite build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ConstantLearning/ ConstantLearning/
COPY tests/load/ tests/load/
COPY docs/dictionary/course.csv docs/dictionary/evidence.jsonl docs/dictionary/readiness.json docs/dictionary/sources-manifest.json docs/dictionary/
COPY --from=frontend /src/ConstantLearning/wwwroot/ ConstantLearning/wwwroot/
RUN dotnet publish tests/load/LoadHost.csproj -c Release -o /app/host /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /app/host/ ./
COPY --from=frontend /src/ConstantLearning/wwwroot/ /app/wwwroot/
ENTRYPOINT ["dotnet", "/app/LoadHost.dll"]
