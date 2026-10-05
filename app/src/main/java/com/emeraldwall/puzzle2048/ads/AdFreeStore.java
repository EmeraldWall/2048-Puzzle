package com.emeraldwall.puzzle2048.ads;

import android.content.Context;
import android.content.SharedPreferences;

/**
 * Local cache of the "remove ads" entitlement. Google Play remains the source of truth;
 * {@link BillingManager} refreshes this value every time it connects to the store.
 */
public final class AdFreeStore
{
    private static final String PREFS = "entitlements";
    private static final String KEY_ADS_REMOVED = "ads_removed";

    private AdFreeStore() {}

    public static boolean isAdsRemoved(Context context)
    {
        return prefs(context).getBoolean(KEY_ADS_REMOVED, false);
    }

    public static void setAdsRemoved(Context context, boolean removed)
    {
        prefs(context).edit().putBoolean(KEY_ADS_REMOVED, removed).apply();
    }

    private static SharedPreferences prefs(Context context)
    {
        return context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }
}
