using Microsoft.AspNetCore.Identity;
using PPMS.Models;

namespace PPMS.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var db = services.GetRequiredService<ApplicationDbContext>();

            string[] roles = { "SuperAdmin", "PrisonAdministrator", "StaffUser" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            if (await userManager.FindByNameAsync("superadmin") == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "superadmin",
                    Email = "admin@ppms.gov",
                    FullName = "System Administrator",
                    Role = "SuperAdmin",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@123456");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "SuperAdmin");
            }

            if (!db.Prisons.Any())
            {
                db.Prisons.AddRange(
                    new Prison { PrisonName = "Bosaso Central Prison", City = "Bosaso", Capacity = 500, CurrentPopulation = 320, SecurityLevel = "Maximum", Address = "Bosaso, Bari Region", ContactNumber = "+252-3-0000001" },
                    new Prison { PrisonName = "Garowe Prison", City = "Garowe", Capacity = 300, CurrentPopulation = 180, SecurityLevel = "Medium", Address = "Garowe, Nugal Region", ContactNumber = "+252-3-0000002" },
                    new Prison { PrisonName = "Galkayo Correctional Facility", City = "Galkayo", Capacity = 250, CurrentPopulation = 150, SecurityLevel = "Medium", Address = "Galkayo, Mudug Region", ContactNumber = "+252-3-0000003" }
                );
                await db.SaveChangesAsync();
            }

            // Seed demo PrisonAdministrator accounts (one per prison) if not already created
            var prisons = db.Prisons.ToList();
            foreach (var prison in prisons)
            {
                var slug = prison.City?.ToLower().Replace(" ", "") ?? prison.Id.ToString();
                var username = $"admin_{slug}";
                if (await userManager.FindByNameAsync(username) == null)
                {
                    var prisonAdmin = new ApplicationUser
                    {
                        UserName = username,
                        Email = $"{slug}@ppms.gov",
                        FullName = $"{prison.PrisonName} Administrator",
                        Role = "PrisonAdministrator",
                        AssignedPrisonId = prison.Id,
                        IsActive = true,
                        EmailConfirmed = true
                    };
                    var res = await userManager.CreateAsync(prisonAdmin, "Admin@123456");
                    if (res.Succeeded)
                        await userManager.AddToRoleAsync(prisonAdmin, "PrisonAdministrator");
                }
            }
        }
    }
}
