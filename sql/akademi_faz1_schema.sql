-- Akademi modülü — Faz 1 (Atama + İzleme + Aktif Ekran Kontrolü)
-- Mevcut tb_Egitim / tb_EgitimKategori tablolarına HİÇ dokunmuyor, hiçbir ilişkisi yok.
-- YbsDB üzerinde çalıştırılmalı (aynı veritabanı, tb_Egitim'in olduğu yer).

CREATE TABLE tb_AkademiEgitim (
    AkademiEgitimID INT IDENTITY(1,1) PRIMARY KEY,
    Baslik          NVARCHAR(300)   NOT NULL,
    Aciklama        NVARCHAR(MAX)   NULL,
    KategoriKodu    NVARCHAR(50)    NULL,          -- Oryantasyon / Mesleki / KisiselGelisim vb. (serbest metin, Faz 1'de sabit liste kod tarafında)
    IcerikTipi      NVARCHAR(20)    NOT NULL,       -- 'Video' | 'Dokuman'
    DosyaUrl        NVARCHAR(500)   NOT NULL,       -- Storage:Modules > AKADEMI altında dosya adı
    SureSaniye      INT             NULL,           -- video toplam süresi; doküman için NULL
    AktifMi         BIT             NOT NULL DEFAULT 1,
    OlusturanSicil  NVARCHAR(20)    NOT NULL,
    KayitTarihi     DATETIME        NOT NULL DEFAULT GETDATE()
);

CREATE TABLE tb_AkademiAtama (
    AtamaID             INT IDENTITY(1,1) PRIMARY KEY,
    AkademiEgitimID     INT             NOT NULL,   -- tb_AkademiEgitim.AkademiEgitimID (FK yok, uygulama katmaninda kontrol edilir)
    SicilNo             NVARCHAR(20)    NOT NULL,
    AtayanSicil         NVARCHAR(20)    NOT NULL,
    AtamaTarihi         DATETIME        NOT NULL DEFAULT GETDATE(),
    SonTarih            DATETIME        NULL,           -- opsiyonel termin
    ZorunluMu           BIT             NOT NULL DEFAULT 1,
    AktifIzlemeZorunlu  BIT             NOT NULL DEFAULT 1,  -- ön planda değilken sayaç durur
    IptalMi             BIT             NOT NULL DEFAULT 0
);
CREATE INDEX IX_AkademiAtama_SicilNo ON tb_AkademiAtama(SicilNo);
CREATE INDEX IX_AkademiAtama_AkademiEgitimID ON tb_AkademiAtama(AkademiEgitimID);

CREATE TABLE tb_AkademiIlerleme (
    IlerlemeID          INT IDENTITY(1,1) PRIMARY KEY,
    AtamaID              INT         NOT NULL,   -- tb_AkademiAtama.AtamaID (FK yok, uygulama katmaninda kontrol edilir)
    MaxIzlenenSaniye      INT         NOT NULL DEFAULT 0,   -- en yüksek erişilen nokta (geri sarma sayılmaz)
    AktifIzlemeSaniye     INT         NOT NULL DEFAULT 0,   -- sadece ön plandayken geçen toplam süre
    TamamlandiMi          BIT         NOT NULL DEFAULT 0,
    TamamlanmaTarihi      DATETIME    NULL,
    GuncellemeTarihi      DATETIME    NOT NULL DEFAULT GETDATE()
);
CREATE UNIQUE INDEX UX_AkademiIlerleme_AtamaID ON tb_AkademiIlerleme(AtamaID);
