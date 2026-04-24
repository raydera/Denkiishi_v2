using Denkiishi_v2;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Denkiishi_v2.Models;
using Denkiishi_v2.Services; // Importante para o WaniKaniService
using Denkiishi_v2.Jobs;
using Denkiishi_v2.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ADICIONAR SERVIÇOS AO CONTAINER ---

builder.Services.AddControllersWithViews();

// Configuração da Conexão com PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<InasDbContext>(options =>
    options.UseNpgsql(connectionString));

// DataProtection: necessário para antiforgery/cookies funcionarem corretamente em Docker/Linux.
// Persistimos chaves em disco para evitar "The antiforgery token could not be decrypted".
var keysDir =
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Denkiishi_v2", "DataProtection-Keys")
        : "/app/.aspnet/DataProtection-Keys";

Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .SetApplicationName("Denkiishi_v2")
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir));

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

// Configurações de e-mail (aceita variáveis de ambiente no padrão ASP.NET: EmailSettings__AppPassword etc.)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Hangfire: Storage no PostgreSQL, schema "hangfire"
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
    config.UsePostgreSqlStorage(connectionString, new PostgreSqlStorageOptions
    {
        SchemaName = "hangfire"
    });
});
builder.Services.AddHangfireServer();
builder.Services.AddScoped<SrsNotificationJob>();

// Registrar Serviços Personalizados
builder.Services.AddHttpClient();
builder.Services.AddScoped<WaniKaniService>(); // Serviço do WaniKani
builder.Services.AddScoped<Denkiishi_v2.Services.KanjiSeedService>();

//import vocabulário
builder.Services.AddScoped<VocabularyImportService>();

// Adicionar o SrsService à Injeção de Dependência
builder.Services.AddScoped<ISrsService, SrsService>();


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

// Hangfire Dashboard (somente Admin)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() }
});

// Configuração de Arquivos Estáticos (Padrão .NET 9)
app.MapStaticAssets();

// Rotas da Aplicação
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Habilita as páginas de Login/Registro do Identity
app.MapRazorPages();

// Recurring Job: notificar SRS a cada 1h
RecurringJob.AddOrUpdate<SrsNotificationJob>(
    "srs-notification-hourly",
    job => job.ExecuteAsync(default),
    Cron.Hourly);

app.Run();
