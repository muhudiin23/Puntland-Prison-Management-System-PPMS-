using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Models;

var builder = WebApplication.CreateBuilder(args);

// Allow large file uploads (evidence docs up to 20 MB each, multiple at once)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200 * 1024 * 1024);

// In development, force HTTP only so an untrusted dev certificate never blocks startup.
// HTTPS is handled by a reverse proxy (IIS/nginx) in production.
if (builder.Environment.IsDevelopment())
    builder.WebHost.UseUrls("http://localhost:5104");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 200 * 1024 * 1024; // 200 MB
    o.ValueLengthLimit = 200 * 1024 * 1024;
});

builder.Services.AddSession();
builder.Services.AddAntiforgery();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(scope.ServiceProvider);

    // One-time migration: move soft-archived prisoners to the new FormerPrisoners table
    var archivedPrisoners = await db.Prisoners
        .Include(p => p.Prison)
        .Include(p => p.Evidences)
        .Where(p => p.IsArchived)
        .ToListAsync();

    if (archivedPrisoners.Count > 0)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            foreach (var p in archivedPrisoners)
            {
                db.FormerPrisoners.Add(new FormerPrisoner
                {
                    PrisonerId             = p.PrisonerId,
                    FullName               = p.FullName,
                    NationalId             = p.NationalId,
                    Gender                 = p.Gender,
                    DateOfBirth            = p.DateOfBirth,
                    CrimeType              = p.CrimeType,
                    SentenceDurationMonths = p.SentenceDurationMonths,
                    EntryDate              = p.EntryDate,
                    ReleaseDate            = p.ReleaseDate,
                    CriminalStatus         = p.CriminalStatus,
                    Address                = p.Address,
                    EmergencyContact       = p.EmergencyContact,
                    PhotoPath              = p.PhotoPath,
                    FingerprintData        = p.FingerprintData,
                    OriginalPrisonId       = p.PrisonId,
                    PrisonName             = p.Prison?.PrisonName ?? string.Empty,
                    PrisonCity             = p.Prison?.City,
                    OriginalCreatedAt      = p.CreatedAt,
                    OriginalUpdatedAt      = p.UpdatedAt,
                    ArchivedAt             = p.ArchivedAt,
                    ArchiveReason          = p.ArchiveReason ?? "Administrative",
                    ArchivedBy             = p.ArchivedBy,
                    ArchiveNotes           = p.ArchiveNotes,
                    EvidenceCount          = p.Evidences.Count,
                    RecordMovedAt          = p.ArchivedAt ?? DateTime.Now
                });
                db.Prisoners.Remove(p);
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to migrate archived prisoners to FormerPrisoners table.");
        }
    }
}

app.Run();
