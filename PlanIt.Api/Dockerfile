FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PlanIt.Api/PlanIt.Api.csproj PlanIt.Api/
RUN dotnet restore PlanIt.Api/PlanIt.Api.csproj

COPY PlanIt.Api/ PlanIt.Api/
RUN dotnet publish PlanIt.Api/PlanIt.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PlanIt.Api.dll"]
