using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPMS.Data;

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

            ViewBag.Query      = query;
            ViewBag.SearchType = searchType;

            // Active prisoners
            var prisonerQuery = _db.Prisoners.Include(p => p.Prison)
                .Where(p => !p.IsArchived).AsQueryable();

            // Archived prisoners
            var archivedQuery = _db.Prisoners.Include(p => p.Prison)
                .Where(p => p.IsArchived).AsQueryable();

            var wantedQuery = _db.WantedCriminals.AsQueryable();

            if (searchType == "name" || string.IsNullOrEmpty(searchType))
            {
                prisonerQuery = prisonerQuery.Where(p => p.FullName.Contains(query));
                archivedQuery = archivedQuery.Where(p => p.FullName.Contains(query));
                wantedQuery   = wantedQuery.Where(w => w.CriminalName.Contains(query));
            }
            else if (searchType == "nationalid")
            {
                prisonerQuery = prisonerQuery.Where(p => p.NationalId.Contains(query));
                archivedQuery = archivedQuery.Where(p => p.NationalId.Contains(query));
                wantedQuery   = wantedQuery.Where(w => w.NationalId.Contains(query));
            }
            else if (searchType == "prisonerid")
            {
                prisonerQuery = prisonerQuery.Where(p => p.PrisonerId.Contains(query));
                archivedQuery = archivedQuery.Where(p => p.PrisonerId.Contains(query));
                wantedQuery   = wantedQuery.Where(w => false);
            }

            ViewBag.Prisoners         = await prisonerQuery.Take(50).ToListAsync();
            ViewBag.ArchivedPrisoners = await archivedQuery.Take(20).ToListAsync();
            ViewBag.WantedCriminals   = await wantedQuery.Take(20).ToListAsync();

            _db.Activities.Add(new Models.Activity
            {
                Description  = $"Search performed: '{query}' (type: {searchType ?? "all"})",
                ActivityType = "Search",
                UserName     = User.Identity?.Name
            });
            await _db.SaveChangesAsync();

            return View();
        }
    }
}
