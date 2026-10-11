using System.Reflection;
using Microsoft.OpenApi.Models;
using TicTacToe.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<GameService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TicTacToe API",
        Version = "v1",
        Description = "REST API for the Tic Tac Toe game. Rows and columns are zero-based (0-2)."
    });
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Swagger UI for manual exploration and testing; development only.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TicTacToe API v1");
        options.DocumentTitle = "TicTacToe API";
    });
}
else
{
    app.UseHsts();
}

// Plain HTTP requests are redirected to HTTPS (https://localhost:7080 in the launch profile).
app.UseHttpsRedirection();
app.UseCors("AngularDev");
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    application = "TicTacToe API",
    status = "Running",
    docs = "Use the /api/games and /api/scoreboard endpoints. Swagger UI: /swagger (Development)."
})).ExcludeFromDescription();

app.Run();

public partial class Program { }
