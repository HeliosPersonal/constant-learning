FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Скопіювати файл проєкту й відновити залежності
COPY ConstantLearning.csproj ./
RUN dotnet restore

# Скопіювати решту вихідного коду
COPY . .

# Зібрати застосунок
RUN dotnet build -c Release -o /app/build

# Опублікувати застосунок
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Образ для запуску
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8888

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConstantLearning.dll"]
