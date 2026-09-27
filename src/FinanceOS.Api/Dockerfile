FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/FinanceOS.Api/FinanceOS.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
CMD ["sh", "-c", "dotnet FinanceOS.Api.dll --urls http://+:${PORT:-5080}"]
