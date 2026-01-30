using Denkiishi_v2;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Denkiishi_v2.Models;
using Denkiishi_v2.Services; // Importante para o WaniKaniService

var builder = WebApplication.CreateBuilder(args);

// --- 1. ADICIONAR SERVIÇOS AO CONTAINER ---

builder.Services.AddControllersWithViews();

// Configuração da Conexão com PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<InasDbContext>(options =>
    options.UseNpgsql(connectionString));

// CONFIGURAÇÃO DO IDENTITY (LOGIN)
// Correção Importante: Usamos IdentityUser para bater certo com o banco de dados
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // Regras de senha simplificadas para facilitar o desenvolvimento
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 3;
})
    .AddRoles<IdentityRole>()
.AddEntityFrameworkStores<InasDbContext>();

// Registrar Serviços Personalizados
builder.Services.AddHttpClient();
builder.Services.AddScoped<WaniKaniService>(); // Serviço do WaniKani
builder.Services.AddScoped<Denkiishi_v2.Services.KanjiSeedService>();

var app = builder.Build();

// --- 2. CONFIGURAR O PIPELINE DE REQUISIÇÕES HTTP ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // O valor default de HSTS é 30 dias. Pode querer mudar para produção.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// --- ORDEM CRUCIAL DE SEGURANÇA ---
app.UseAuthentication(); // 1º: Verifica QUEM é o utilizador (Login)
app.UseAuthorization();  // 2º: Verifica O QUE ele pode fazer (Permissões)
// ----------------------------------

// Configuração de Arquivos Estáticos (Padrão .NET 9)
app.MapStaticAssets();

// Rotas da Aplicação
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Habilita as páginas de Login/Registro do Identity
app.MapRazorPages();

app.Run();
