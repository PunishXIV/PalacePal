FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /build
COPY Pal.Common/Pal.Common.csproj Pal.Common/
COPY Pal.Server/Pal.Server.csproj Pal.Server/
RUN dotnet restore Pal.Server/Pal.Server.csproj

COPY . ./
RUN dotnet publish Pal.Server/Pal.Server.csproj --configuration Release --no-restore -o /dist

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
EXPOSE 5415
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=
ENV DataDirectory=/data
ENV UseForwardedIp=true
ENV Kestrel__Endpoints__Http2__Url=http://+:5415

RUN adduser --uid 2000 --disabled-password --group --no-create-home --quiet --system pal

WORKDIR /app
COPY --from=build-env /dist .

USER pal
ENTRYPOINT ["dotnet", "Pal.Server.dll"]