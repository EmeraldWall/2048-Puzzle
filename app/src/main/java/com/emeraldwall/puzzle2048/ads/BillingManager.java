package com.emeraldwall.puzzle2048.ads;

import android.app.Activity;
import android.content.Context;

import androidx.annotation.NonNull;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.PurchasesUpdatedListener;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;
import com.emeraldwall.puzzle2048.BuildConfig;
import com.emeraldwall.puzzle2048.R;

import java.util.Collections;
import java.util.List;

/**
 * Google Play Billing wrapper for the single one-time "remove ads" product.
 * Create in onCreate, call {@link #destroy()} in onDestroy.
 */
public final class BillingManager implements PurchasesUpdatedListener
{
    public interface Listener
    {
        /** The entitlement changed or was (re)confirmed. */
        void onAdsRemovedChanged(boolean removed);

        /** Localized price such as "$2.99" became available. */
        void onPriceLoaded(String formattedPrice);

        /** Something the user should be told about. */
        void onMessage(int stringResId);
    }

    private final Context mContext;
    private final Listener mListener;
    private final BillingClient mClient;
    private ProductDetails mProduct;

    public BillingManager(Context context, Listener listener)
    {
        mContext = context.getApplicationContext();
        mListener = listener;
        mClient = BillingClient.newBuilder(mContext)
                .setListener(this)
                .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
                .build();
        connect();
    }

    public void destroy()
    {
        mClient.endConnection();
    }

    /** Re-reads purchases from Google Play (restore purchases). */
    public void restore()
    {
        if (!mClient.isReady())
        {
            connect();
            mListener.onMessage(R.string.store_unavailable);
            return;
        }
        queryOwnedPurchases();
    }

    public void buy(Activity activity)
    {
        if (!mClient.isReady() || mProduct == null)
        {
            connect();
            mListener.onMessage(R.string.store_unavailable);
            return;
        }

        BillingFlowParams params = BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(Collections.singletonList(
                        BillingFlowParams.ProductDetailsParams.newBuilder()
                                .setProductDetails(mProduct)
                                .build()))
                .build();
        mClient.launchBillingFlow(activity, params);
    }

    private void connect()
    {
        if (mClient.getConnectionState() == BillingClient.ConnectionState.CONNECTING
                || mClient.isReady())
            return;

        mClient.startConnection(new BillingClientStateListener()
        {
            @Override
            public void onBillingSetupFinished(@NonNull BillingResult result)
            {
                if (result.getResponseCode() != BillingClient.BillingResponseCode.OK)
                    return;
                queryOwnedPurchases();
                queryProduct();
            }

            @Override
            public void onBillingServiceDisconnected() { /* reconnected on next use */ }
        });
    }

    private void queryProduct()
    {
        QueryProductDetailsParams.Product product = QueryProductDetailsParams.Product.newBuilder()
                .setProductId(BuildConfig.REMOVE_ADS_PRODUCT_ID)
                .setProductType(BillingClient.ProductType.INAPP)
                .build();

        mClient.queryProductDetailsAsync(
                QueryProductDetailsParams.newBuilder()
                        .setProductList(Collections.singletonList(product)).build(),
                (result, products) ->
                {
                    if (result.getResponseCode() != BillingClient.BillingResponseCode.OK
                            || products.isEmpty())
                        return;

                    mProduct = products.get(0);
                    ProductDetails.OneTimePurchaseOfferDetails offer = mProduct.getOneTimePurchaseOfferDetails();
                    if (offer != null)
                        mListener.onPriceLoaded(offer.getFormattedPrice());
                });
    }

    private void queryOwnedPurchases()
    {
        mClient.queryPurchasesAsync(
                QueryPurchasesParams.newBuilder().setProductType(BillingClient.ProductType.INAPP).build(),
                (result, purchases) ->
                {
                    // Only trust a successful answer; an error must never take the entitlement away.
                    if (result.getResponseCode() == BillingClient.BillingResponseCode.OK)
                        applyPurchases(purchases);
                });
    }

    @Override
    public void onPurchasesUpdated(@NonNull BillingResult result, List<Purchase> purchases)
    {
        int code = result.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null)
            applyPurchases(purchases);
        else if (code == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED)
            queryOwnedPurchases();
        else if (code != BillingClient.BillingResponseCode.USER_CANCELED)
            mListener.onMessage(R.string.purchase_failed);
    }

    private void applyPurchases(List<Purchase> purchases)
    {
        boolean owned = false;
        boolean pending = false;

        for (Purchase purchase : purchases)
        {
            if (!purchase.getProducts().contains(BuildConfig.REMOVE_ADS_PRODUCT_ID))
                continue;

            if (purchase.getPurchaseState() == Purchase.PurchaseState.PURCHASED)
            {
                owned = true;
                acknowledgeIfNeeded(purchase);
            }
            else if (purchase.getPurchaseState() == Purchase.PurchaseState.PENDING)
                pending = true;
        }

        AdFreeStore.setAdsRemoved(mContext, owned);
        mListener.onAdsRemovedChanged(owned);
        if (pending && !owned)
            mListener.onMessage(R.string.purchase_pending);
    }

    private void acknowledgeIfNeeded(Purchase purchase)
    {
        if (purchase.isAcknowledged())
            return;

        mClient.acknowledgePurchase(
                AcknowledgePurchaseParams.newBuilder().setPurchaseToken(purchase.getPurchaseToken()).build(),
                result -> { /* retried by the next purchase query if it failed */ });
    }
}
