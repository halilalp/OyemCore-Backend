using System;
using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Dtos
{
    public class PersonelPerformansRaporuDto
    {
        public string Sicil { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Dept { get; set; }
        public int OpenTasks { get; set; }
        public int Tamamlanan { get; set; }
        public int Bekleyen { get; set; }
        public int OnayBekleyen { get; set; }
        public string AvgResolve { get; set; }
        public double OrtalamaHizDouble { get; set; }
        public string Cost { get; set; }
        public double Rating { get; set; }
    }

    public class BakimDashboardMonthStatsDto
    {
        public int Month { get; set; }
        public string MonthName { get; set; }
        public int TotalCount { get; set; }
        public int ElectricCount { get; set; }
        public int MechanicCount { get; set; }
        public int CompletedCount { get; set; }
        public int RemainingCount { get; set; }
        public double DowntimeHours { get; set; }
        public double MttrTotalHours { get; set; }
        public double MttrAvgHours { get; set; }
        public double MtbfHours { get; set; }
    }

    public class BakimDashboardStatsDto
    {
        public int Year { get; set; }
        public List<BakimDashboardMonthStatsDto> Data { get; set; }
    }
}
