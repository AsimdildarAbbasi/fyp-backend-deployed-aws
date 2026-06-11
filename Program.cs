using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ObmanagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ ADD THIS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Optional: often helps with CORS issues on local
        });
});

var app = builder.Build();

// Force Swagger to open regardless of Environment mode so we can test it
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BIIT OBMS API v1");
    c.RoutePrefix = string.Empty; // This makes Swagger load directly at http://localhost:5077/
});
// Only use HTTPS redirection if we aren't in a Docker container
if (!Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
{
    app.UseHttpsRedirection();
}

// ✅ ADD THIS (ORDER MATTERS)
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();