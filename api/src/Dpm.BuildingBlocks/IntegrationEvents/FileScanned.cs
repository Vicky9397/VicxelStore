using Dpm.BuildingBlocks.Domain;

namespace Dpm.BuildingBlocks.IntegrationEvents;

/// <summary>
/// Published by the Files module when a scan completes. Catalog consumes it to
/// maintain its variant readiness projection.
///
/// Cross-module event contracts live in the shared kernel rather than in the
/// publishing module: Files sits downstream of Catalog in the dependency graph
/// (spec 11B section 5), so Catalog referencing a Files assembly would reverse
/// an arrow the spec forbids. Both modules already depend on BuildingBlocks.
/// </summary>
/// <param name="CleanFileCountForVariant">
/// How many files of this variant have passed the scan, counted by Files after
/// applying this outcome. Carrying the total rather than a delta keeps the
/// consumer idempotent under at-least-once delivery.
/// </param>
public sealed record FileScanned(
    Guid FilePublicId,
    Guid VariantPublicId,
    bool IsClean,
    int CleanFileCountForVariant) : DomainEvent;
