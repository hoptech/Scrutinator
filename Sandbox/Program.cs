using Sandbox.Services;
using Scrutinator.Middleware.DI;
using Scrutinator.Middleware.Log;
using Scrutinator.Middleware.Package;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ISingletonDependency, SingletonDependency>();
builder.Services.AddScoped<IScopedDependency, ScopedDependency>();
builder.Services.AddTransient<ITransientDependency, TransientDependency>();

builder.Services.AddDIScrutinator();
builder.Services.AddLogScrutinator();

builder.Services.AddHostedService<TestHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDIScrutinator(opts => {
    opts.OpenDashboardAutomatically = true;
    opts.IncludeSystemServices = false;
});

app.UseLogScrutinator(opts =>
{
    opts.OpenDashboardAutomatically = true;
});

app.UsePackageScrutinator(opts =>
{
    opts.OpenDashboardAutomatically = true;
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
