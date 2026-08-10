using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IBordroService
    {
        (int total, IEnumerable<object> data) GetBordroListesiUser(int kullaniciID, int pageIndex, int pageSize);
        bool BordroAksiyonKaydet(int kullaniciID, int bordroID, string aksiyon);
    }
}
