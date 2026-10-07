using Scalar.AspNetCore;
using WowTts.Application;
using WowTts.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services. The Api layer only calls these two methods.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document lists every endpoint. Scalar shows it as a page where you can try them.
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
