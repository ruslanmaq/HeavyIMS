using HeavyIMS.Application.Interfaces;
using HeavyIMS.Application.Services;
using HeavyIMS.Domain.Events;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using HeavyIMS.Infrastructure.Events;
using HeavyIMS.Infrastructure.Events.Handlers;
using HeavyIMS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure Database
builder.Services.AddDbContext<HeavyIMSDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// Register Unit of Work and Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPartRepository, PartRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<ITechnicianRepository, TechnicianRepository>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();

// Register Domain Event Infrastructure
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

// Register Domain Event Handlers
builder.Services.AddScoped<IDomainEventHandler<InventoryLowStockDetected>, InventoryLowStockDetectedHandler>();
// TODO: Add more event handlers as they are implemented:
// builder.Services.AddScoped<IDomainEventHandler<PartPriceUpdated>, PartPriceUpdatedHandler>();
// builder.Services.AddScoped<IDomainEventHandler<PartDiscontinued>, PartDiscontinuedHandler>();
// builder.Services.AddScoped<IDomainEventHandler<WorkOrderStatusChanged>, WorkOrderStatusChangedHandler>();

// Register Application Services
builder.Services.AddScoped<IPartService, PartService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
// TODO: Add IWorkOrderService, ITechnicianService when implemented

// Optional: Configure Redis Cache (if available)
// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration = builder.Configuration.GetConnectionString("Redis");
// });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
