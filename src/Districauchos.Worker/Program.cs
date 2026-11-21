using Districauchos.Core;
using Districauchos.Data;
using Districauchos.Search;
using Districauchos.Sheets;
using Districauchos.Worker;

var builder = Host.CreateApplicationBuilder(args);

var settings = AppSettings.From(builder.Configuration);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(_ => SheetSourceFactory.Create(settings));
builder.Services.AddSingleton(_ => CatalogRepository.CreateDataSource(
    settings.DatabaseUrl ?? throw new InvalidOperationException("Falta DATABASE_URL")));
builder.Services.AddSingleton<CatalogRepository>();
builder.Services.AddSingleton(_ => MeiliClient.Create(settings));
builder.Services.AddSingleton<ProductIndex>();
builder.Services.AddSingleton<StockCheckRepository>();
builder.Services.AddHostedService<SyncBackgroundService>();
builder.Services.AddHostedService<StockCheckService>();
builder.Services.AddHostedService<DrivePhotoService>();

builder.Build().Run();
