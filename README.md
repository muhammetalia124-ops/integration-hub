# IntegrationHub

Döviz, altın, hava durumu ve namaz vakitleri verilerini toplayan, kullanıcı
girişi ve admin yönetimi sunan ASP.NET Core tabanlı bir REST API.

## Mimari

- **IntegrationHub.Domain** – Varlıklar (User, UserActivity, CurrencyRate vb.)
- **IntegrationHub.Application** – Servisler, arayüzler ve DTO'lar
- **IntegrationHub.Infrastructure** – EF Core, dış API entegrasyonları, JWT
- **IntegrationHub.Api** – Controller'lar, middleware'lar ve giriş noktası

## Gereksinimler

- .NET 10 SDK
- PostgreSQL

## Kurulum

1. Veritabanı bağlantı bilgilerini user secrets içine ekleyin:

```
dotnet user-secrets set "ConnectionStrings:UserDbConnection" "..."
dotnet user-secrets set "ConnectionStrings:ActivityDbConnection" "..."
dotnet user-secrets set "ConnectionStrings:CurrencyRatesConnection" "..."
dotnet user-secrets set "JwtSettings:SecretKey" "..."
dotnet user-secrets set "AdminBootstrap:Email" "..."
dotnet user-secrets set "AdminBootstrap:Username" "..."
dotnet user-secrets set "AdminBootstrap:Password" "..."
```

2. Uygulamayı çalıştırın:

```
dotnet run --project IntegrationHub.Api
```

Başlangıçta migration'lar otomatik uygulanır ve ilk yönetici hesabı oluşturulur.

## Arayüz

Uygulama `http://localhost:5080` adresinde çalışır, Swagger yalnızca
Development ortamında açılır. Statik arayüz `wwwroot` klasöründen sunulur.
