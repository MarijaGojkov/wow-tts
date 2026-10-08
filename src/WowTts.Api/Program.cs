using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using WowTts.Api.ErrorHandling;
using WowTts.Application;
using WowTts.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services. The Api layer only calls these two methods.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Enums such as QuestPart are written as text ("accept") instead of numbers in JSON.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();

// Errors from any layer become ProblemDetails responses (see GlobalExceptionHandler).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document lists every endpoint. Scalar shows it as a page where you can try them.
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
