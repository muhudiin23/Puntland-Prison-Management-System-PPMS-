using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;
using PPMS.Helpers;

namespace PPMS.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SearchController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? query, string? searchType)
        {
            if (string.IsNullOrWhiteSpace(query))
                return View();

            var userPrisonId = User.PrisonId();
            var superAdmin = User.IsSuperAdmin();

            ViewBag.Query      = query;
            ViewBag.SearchType = searchType;

            // Active prisoners — scoped to user's prison
            var prisonerQuery = _db.Prisoners.Include(p => p.Prison)
                .Where(p => !p.IsArchived).AsQueryable();

            if (!superAdmin && userPrisonId.HasValue)
                prisonerQuery = prisonerQuery.Where(p => p.PrisonId == userPrisonId.Value);

            // FormerPrisoners (archive table) — scoped to user's prison
            var formerQuery = _db.FormerPrisoners.AsQueryable();
            if (!superAdmin && userPrisonId.HasValue)
                formerQuery = formerQuery.Where(p => p.OriginalPrisonId == userPrisonId.Value);

            // WantedCriminals are global (no PrisonId)
            var wantedQuery = _db.WantedCriminals.AsQueryable();

            if (searchType == "name" || string.IsNullOrEmpty(searchType))
            {
                prisonerQuery = prisonerQuery.Where(p => p.FullName.Contains(query));
                formerQuery   = formerQuery.Where(p => p.FullName.Contains(query));
                wantedQuery   = wantedQuery.Where(w => w.CriminalName.Contains(query));
            }
            else if (searchType == "nationalid")
            {
                prisonerQuery = prisonerQuery.Where(p => p.NationalId.Contains(query));
                formerQuery   = formerQuery.Where(p => p.NationalId.Contains(query));
                wantedQuery   = wantedQuery.Where(w => w.NationalId.Contains(query));
            }
            else if (searchType == "prisonerid")
            {
                prisonerQuery = prisonerQuery.Where(p => p.PrisonerId.Contains(query));
                formerQuery   = formerQuery.Where(p => p.PrisonerId.Contains(query));
                wantedQuery   = wantedQuery.Where(w => false);
            }

            ViewBag.Prisoners         = await prisonerQuery.Take(50).ToListAsync();
            ViewBag.ArchivedPrisoners = await formerQuery.Take(20).ToListAsync();
            ViewBag.WantedCriminals   = await wantedQuery.Take(20).ToListAsync();

            _db.Activities.Add(new Models.Activity
            {
                Description  = $"Search performed: '{query}' (type: {searchType ?? "all"})",
                ActivityType = "Search",
                UserName     = User.Identity?.Name,
                PrisonId     = userPrisonId
            });
            await _db.SaveChangesAsync();

            return View();
        }
    }
}
