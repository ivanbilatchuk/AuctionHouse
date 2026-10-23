namespace Catalog.Domain.Entities;

public enum ItemStatus
{
    Draft,
    Listed,
    InAuction,
    Sold,
    Withdrawn
}

public enum ItemCondition
{
    Mint,
    NearMint,
    Excellent,
    Good,
    Fair,
    Poor
}
