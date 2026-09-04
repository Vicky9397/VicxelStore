using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.IntegrationEvents;
using Dpm.Catalog.Application.Abstractions;
using MediatR;

namespace Dpm.Catalog.Application.Projections;

/// <summary>
/// Keeps the variant readiness projection in step with scan outcomes from the
/// Files module. This is how Catalog learns whether a variant is deliverable
/// without depending on Files, which sits downstream of it.
///
/// The event carries the resulting total rather than a delta, so replaying it
/// under at-least-once delivery converges on the same value.
/// </summary>
public sealed class FileScannedHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IClock clock)
    : INotificationHandler<FileScanned>
{
    public async Task Handle(FileScanned notification, CancellationToken cancellationToken)
    {
        var product = await products.FindByVariantPublicIdAsync(notification.VariantPublicId, cancellationToken);
        if (product is null)
        {
            return;
        }

        product.ApplyVariantFileReadiness(
            notification.VariantPublicId,
            notification.CleanFileCountForVariant,
            clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
