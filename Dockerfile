FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/CampaignStudio.Core/ src/CampaignStudio.Core/
COPY src/CampaignStudio.Api/ src/CampaignStudio.Api/
RUN dotnet publish src/CampaignStudio.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080 Studio__DataDirectory=/app/data
EXPOSE 8080
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
ENTRYPOINT ["dotnet", "CampaignStudio.Api.dll"]
