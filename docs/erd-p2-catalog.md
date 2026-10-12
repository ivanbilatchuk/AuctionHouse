# ERD — Проєкт №2 (Catalog, SQL Server + EF Core)

> Частина документації проєкту AuctionHouse. Повний опис і порядок запуску: [README.md](../README.md).

![erd-p2-catalog](img/erd-p2-catalog.png)

*Якщо діаграма не відображається, зображення вище — той самий ERD; текстове джерело (Mermaid) нижче.*

```mermaid
erDiagram
    Category ||--o{ Category : "1:N parent -> children"
    Category ||--o{ Item : "1:N"
    Item ||--|| ItemDetail : "1:1"
    Item ||--o{ ItemImage : "1:N"
    Item ||--o{ ItemTag : "M:N via"
    Tag ||--o{ ItemTag : "M:N via"

    Category {
        bigint CategoryId PK
        bigint ParentCategoryId FK "self"
        nvarchar Name
        nvarchar Slug UK
        nvarchar AttributeKind "cards/stamps/vinyl/coins/generic"
        int SortOrder
        bit IsDeleted
        rowversion RowVersion
    }
    Item {
        bigint ItemId PK
        bigint CategoryId FK
        uniqueidentifier SellerExternalId "Identity, no FK"
        nvarchar Title
        nvarchar Slug UK
        decimal EstimateLow "CHECK > 0"
        decimal EstimateHigh "CHECK >= EstimateLow"
        char Currency
        nvarchar Status
        bit IsDeleted
        rowversion RowVersion
    }
    ItemDetail {
        bigint ItemId PK "PK = FK (1:1)"
        nvarchar Condition "CHECK enum"
        smallint ProductionYear
        nvarchar CertificateNumber UK "filtered unique"
        nvarchar Provenance
        nvarchar Description
    }
    ItemImage {
        bigint ImageId PK
        bigint ItemId FK
        nvarchar Url
        bit IsPrimary "max 1 per item"
        int SortOrder
    }
    Tag {
        bigint TagId PK
        nvarchar Name UK
        nvarchar Slug UK
    }
    ItemTag {
        bigint ItemId PK "FK"
        bigint TagId PK "FK"
        datetime2 AddedAt "own field"
        nvarchar AddedBy "own field"
    }
```

**6 сутностей, усі типи зв'язків:**

| Тип | Де | Примітка |
|---|---|---|
| **1:1** | `Item` ↔ `ItemDetail` | PK залежної таблиці = FK; `ON DELETE CASCADE` |
| **1:N** | `Category` → `Item`; `Item` → `ItemImage`; `Category` → `Category` (ієрархія) | FK на боці «багатьох» |
| **M:N з власним полем** | `Item` ↔ `Tag` через `ItemTag` | проміжна сутність має власні поля **`AddedAt`** (дата додавання) і **`AddedBy`** |
