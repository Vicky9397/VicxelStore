namespace Dpm.Catalog.Domain;

/// <summary>
/// Moderation lifecycle (spec 05 section 5.3, 02 section 2.4 Product Management):
/// Draft -> Submitted -> InReview -> Approved -> Published, with Rejected and
/// Archived as terminal-ish branches.
/// </summary>
public enum ProductStatus : byte
{
    Draft = 1,
    Submitted = 2,
    InReview = 3,
    Approved = 4,
    Published = 5,
    Rejected = 6,
    Archived = 7,
}
