namespace PPMS.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalPrisoners { get; set; }
        public int TotalStaff { get; set; }
        public int TotalPrisons { get; set; }
        public int WantedCriminals { get; set; }
        public List<Alert> RecentAlerts { get; set; } = new();
        public List<Activity> RecentActivities { get; set; } = new();
        public int ActiveAlerts { get; set; }
        public int PrisonersReleasedThisMonth { get; set; }
        public int FormerPrisonersCount { get; set; }
        public int TotalCaseReports { get; set; }
        public int OpenCaseReports { get; set; }
    }
}
