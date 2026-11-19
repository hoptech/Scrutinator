using Sandbox.Services;
using Scrutinator.Middleware.DI;
using Scrutinator.Middleware.Log;
using Scrutinator.Middleware.Package;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var openDashboardAutomatically = builder.Configuration["Scrutinator:OpenDashboardAutomatically"] is { } autoOpenValue
    ? bool.Parse(autoOpenValue)
    : true;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Scrutinator Sandbox",
        Version = "v1"
    });
});

builder.Services.AddSingleton<ISingletonDependency, SingletonDependency>();
builder.Services.AddScoped<IScopedDependency, ScopedDependency>();
builder.Services.AddTransient<ITransientDependency, TransientDependency>();

builder.Services.AddScrutinatorDI();
builder.Services.AddScrutinatorLog();

builder.Services.AddHostedService<TestHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseScrutinatorDI(opts => {
    opts.OpenDashboardAutomatically = openDashboardAutomatically;
    opts.IncludeSystemServices = false;
});

app.UseScrutinatorLog(opts =>
{
    opts.OpenDashboardAutomatically = openDashboardAutomatically;
});

app.UseScrutinatorPackage(opts =>
{
    opts.OpenDashboardAutomatically = openDashboardAutomatically;
});

app.MapGet("/sandbox", () => "Hello! Scrutinator is watching.");

Task.Run(async () => {
    while(true) {
        // Test INFO (Green)
        app.Logger.LogInformation("Scrutinator Heartbeat at {time}", DateTime.Now);
        
        // Test WARNING (Yellow) every 4 seconds
        if (DateTime.Now.Second % 4 == 0) 
        {
            app.Logger.LogWarning("System is running a bit slow...");
        }

        // Test ERROR (Red) every 10 seconds
        if (DateTime.Now.Second % 10 == 0)
        {
            app.Logger.LogError(new Exception("Fake DB Error"), "Database connection failed!");
        }

        await Task.Delay(2_000);
    }
});

app.Run();
