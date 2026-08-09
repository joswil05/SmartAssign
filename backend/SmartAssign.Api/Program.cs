using Microsoft.EntityFrameworkCore;
using SmartAssign.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// El ORM no decide reglas de negocio (05_TRD.md §1.4): la escritura sobre
// tablas críticas queda denegada a esta cuenta desde la etapa E4
// (04_ESQUEMA_BACKEND.md §7.5). Aquí solo se registra el mapeador.
builder.Services.AddDbContext<SmartAssignDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SmartAssignDb")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
