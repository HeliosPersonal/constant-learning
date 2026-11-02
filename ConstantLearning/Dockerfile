FROM node:22-bookworm-slim AS miniapp
WORKDIR /src/ConstantLearning.MiniApp
COPY ConstantLearning.MiniApp/package.json ConstantLearning.MiniApp/package-lock.json ./
RUN npm ci
COPY ConstantLearning.MiniApp/ ./
COPY docs/features/personal-trainer-mvp/design/theme-tokens.json /src/docs/features/personal-trainer-mvp/design/theme-tokens.json
# Образ лише пакує Mini App; локальні перевірки фронтенду описані окремо.
RUN npm run generate:tokens && npx vite build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ConstantLearning/ConstantLearning.csproj ConstantLearning/
RUN dotnet restore ConstantLearning/ConstantLearning.csproj
COPY ConstantLearning/ ConstantLearning/
COPY docs/dictionary/course.csv docs/dictionary/evidence.jsonl docs/dictionary/readiness.json docs/dictionary/sources-manifest.json docs/dictionary/
COPY --from=miniapp /src/ConstantLearning/wwwroot/ ConstantLearning/wwwroot/
RUN dotnet publish ConstantLearning/ConstantLearning.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8888 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Перевірка системних даних; виконуваний smoke додатково перевіряє .NET та NodaTime.
RUN dpkg-query -W tzdata libicu74 \
    && test -f /usr/share/zoneinfo/Europe/Warsaw \
    && test -f /usr/share/zoneinfo/Asia/Tokyo
EXPOSE 8888
COPY --from=build /app/publish/ ./

# Тестовий target із тим самим runtime та опублікованими файлами застосунку.
FROM build AS smoke-build
COPY tools/deployment/ContainerSmoke/ tools/deployment/ContainerSmoke/
RUN dotnet publish tools/deployment/ContainerSmoke/ContainerSmoke.csproj -c Release -o /app/smoke /p:UseAppHost=false

FROM runtime AS container-smoke
COPY --from=smoke-build /app/smoke/ /smoke/
USER $APP_UID
ENTRYPOINT ["dotnet", "/smoke/ContainerSmoke.dll"]

# За замовчуванням збирається production-образ без тестових залежностей.
FROM runtime AS final
USER $APP_UID
ENTRYPOINT ["dotnet", "ConstantLearning.dll"]
