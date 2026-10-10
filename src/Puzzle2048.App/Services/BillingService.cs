using Plugin.InAppBilling;
using Puzzle2048.Core;
using System.Net.Http;

namespace Puzzle2048.App.Services;

/// <summary>
/// The one-time "Remove ads" purchase through Google Play Billing. Google Play is the source of truth:
/// the phone only keeps a local copy so the ads stay off while offline.
/// </summary>
public sealed class BillingService
{
    private readonly ProgressStore _progress;
    private bool _initialized;

    public BillingService(ProgressStore progress) => _progress = progress;

    /// <summary>Raised on the main thread whenever the ads setting changes.</summary>
    public event Action? AdsRemovedChanged;

    /// <summary>Localized price such as "$2.99", once the store has answered.</summary>
    public string? Price { get; private set; }

    public bool AdsRemoved => _progress.AdsRemoved;

    /// <summary>Connects to Google Play, reads the price and the player's purchases. Safe to call repeatedly.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            IInAppBilling billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync())
                return;

            _initialized = true;
            await LoadPriceAsync(billing);
            await RefreshPurchasesAsync(billing);
        }
        catch (Exception ex) when (IsBillingFailure(ex))
        {
            // No Play Store on this phone or no network: keep the saved state
            System.Diagnostics.Debug.WriteLine($"Billing unavailable: {ex.Message}");
        }
    }

    /// <summary>Starts the Google Play purchase screen. Returns true when ads are now removed.</summary>
    public async Task<bool> BuyAsync()
    {
        try
        {
            await InitializeAsync();
            IInAppBilling billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync())
                return false;

            InAppBillingPurchase? purchase = await billing.PurchaseAsync(AppConstants.RemoveAdsProductId, ItemType.InAppPurchase);
            if (purchase is { State: PurchaseState.Purchased })
            {
                await AcknowledgeAsync(billing, purchase);
                SetAdsRemoved(true);
                return true;
            }
        }
        catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.AlreadyOwned)
        {
            SetAdsRemoved(true);
            return true;
        }
        catch (Exception ex) when (IsBillingFailure(ex))
        {
            System.Diagnostics.Debug.WriteLine($"Purchase failed: {ex.Message}");
        }

        return AdsRemoved;
    }

    /// <summary>Re-reads the purchases from Google Play (restore purchase).</summary>
    public async Task<bool> RestoreAsync()
    {
        try
        {
            IInAppBilling billing = CrossInAppBilling.Current;
            if (await billing.ConnectAsync())
                await RefreshPurchasesAsync(billing);
        }
        catch (Exception ex) when (IsBillingFailure(ex))
        {
            System.Diagnostics.Debug.WriteLine($"Restore failed: {ex.Message}");
        }

        return AdsRemoved;
    }

    private async Task LoadPriceAsync(IInAppBilling billing)
    {
        IEnumerable<InAppBillingProduct> products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, [AppConstants.RemoveAdsProductId]);
        Price = products.FirstOrDefault()?.LocalizedPrice;
        MainThread.BeginInvokeOnMainThread(() => AdsRemovedChanged?.Invoke());
    }

    private async Task RefreshPurchasesAsync(IInAppBilling billing)
    {
        // Only trust a successful answer: a failed query must never take the purchase away
        IEnumerable<InAppBillingPurchase> purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
        InAppBillingPurchase? owned = purchases.FirstOrDefault(p =>
            p.ProductId == AppConstants.RemoveAdsProductId && p.State == PurchaseState.Purchased);

        if (owned is not null)
            await AcknowledgeAsync(billing, owned);

        SetAdsRemoved(owned is not null);
    }

    private static async Task AcknowledgeAsync(IInAppBilling billing, InAppBillingPurchase purchase)
    {
        if (purchase.IsAcknowledged == true)
            return;

        // Google refunds purchases that are not acknowledged within three days
        await billing.FinalizePurchaseAsync([purchase.PurchaseToken ?? purchase.TransactionIdentifier]);
    }

    /// <summary>Store problems (no Play Store, no network, cancelled) must never crash the game.</summary>
    private static bool IsBillingFailure(Exception ex) =>
        ex is InAppBillingPurchaseException or InvalidOperationException or NotSupportedException or TaskCanceledException or HttpRequestException;

    private void SetAdsRemoved(bool removed)
    {
        if (_progress.AdsRemoved == removed)
            return;

        _progress.AdsRemoved = removed;
        MainThread.BeginInvokeOnMainThread(() => AdsRemovedChanged?.Invoke());
    }
}
