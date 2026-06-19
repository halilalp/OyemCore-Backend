using System;
using System.Collections.Generic;

namespace WebPortalSpace.BusinessLayer.Dtos
{
    public class SavePlanRequest
    {
        public string PlanKodu { get; set; }
        public string HatKodu { get; set; }
        public string BakimTuru { get; set; }
        public string HedefBaslangic { get; set; }
        public string HedefBitis { get; set; }
    }

    public class UpdatePlanStatusRequest
    {
        public string Durum { get; set; }
        public string Not { get; set; }
        public string DosyaUrl { get; set; }
    }

    public class SavePeriyodikRequest
    {
        public string KontrolKodu { get; set; }
        public string BolumKodu { get; set; }
        public string KontrolTuru { get; set; }
        public string HedefBaslangic { get; set; }
        public string HedefBitis { get; set; }
        public string Aciklama { get; set; }
    }

    public class UpdatePeriyodikStatusRequest
    {
        public string Durum { get; set; }
        public string Aciklama { get; set; }
    }

    public class SaveSarfiyatRequest
    {
        public string MalzemeKodu { get; set; }
        public decimal Miktar { get; set; }
        public string MakineKodu { get; set; }
    }

    public class SavePeriyodikGelismeRequest
    {
        public string Aciklama { get; set; }
        public string DosyaUrl { get; set; }
    }

    // Response DTOs for generic paginated lists
    public class PaginatedListDto<T>
    {
        public IEnumerable<T> Data { get; set; }
        public int TotalCount { get; set; }
    }

    // Response DTO for material searches
    public class MalzemeSearchResponseDto
    {
        public IEnumerable<MalzemeDto> Results { get; set; }
        public MalzemePaginationDto Pagination { get; set; }
    }

    public class MalzemePaginationDto
    {
        public bool More { get; set; }
    }
}
