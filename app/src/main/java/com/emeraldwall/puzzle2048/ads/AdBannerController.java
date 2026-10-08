package com.emeraldwall.puzzle2048.ads;

import android.app.Activity;
import android.util.DisplayMetrics;
import android.view.View;
import android.widget.FrameLayout;

import com.emeraldwall.puzzle2048.BuildConfig;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.AdSize;
import com.google.android.gms.ads.AdView;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.RequestConfiguration;

import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Owns the permanent bottom banner of one screen. Collapses the container completely
 * when the user has purchased "remove ads".
 *
 * The game is aimed at children, so every ad request is tagged as child-directed and capped
 * at the G rating (Google Play Families policy). That makes ads non-personalized and means
 * no consent form is shown.
 */
public final class AdBannerController
{
    private static final AtomicBoolean sMobileAdsStarted = new AtomicBoolean(false);

    private final Activity mActivity;
    private final FrameLayout mContainer;
    private AdView mAdView;
    private boolean mDestroyed;

    public AdBannerController(Activity activity, FrameLayout container)
    {
        mActivity = activity;
        mContainer = container;
        refresh();
    }

    /** Call from onResume: applies a purchase or refund that happened in the meantime. */
    public void resume()
    {
        refresh();
        if (mAdView != null)
            mAdView.resume();
    }

    public void pause()
    {
        if (mAdView != null)
            mAdView.pause();
    }

    public void destroy()
    {
        mDestroyed = true;
        removeBanner();
    }

    private void refresh()
    {
        if (AdFreeStore.isAdsRemoved(mActivity))
        {
            removeBanner();
            mContainer.setVisibility(View.GONE);
            return;
        }

        mContainer.setVisibility(View.VISIBLE);
        if (mAdView == null)
            loadBanner();
    }

    private void loadBanner()
    {
        if (mDestroyed || mAdView != null || mActivity.isFinishing())
            return;
        if (AdFreeStore.isAdsRemoved(mActivity))
            return;

        if (sMobileAdsStarted.compareAndSet(false, true))
        {
            // Must be set before the SDK starts and before any ad is requested
            MobileAds.setRequestConfiguration(new RequestConfiguration.Builder()
                    .setTagForChildDirectedTreatment(RequestConfiguration.TAG_FOR_CHILD_DIRECTED_TREATMENT_TRUE)
                    .setTagForUnderAgeOfConsent(RequestConfiguration.TAG_FOR_UNDER_AGE_OF_CONSENT_TRUE)
                    .setMaxAdContentRating(RequestConfiguration.MAX_AD_CONTENT_RATING_G)
                    .build());
            MobileAds.initialize(mActivity.getApplicationContext());
        }

        mAdView = new AdView(mActivity);
        mAdView.setAdUnitId(BuildConfig.ADMOB_BANNER_UNIT_ID);
        mAdView.setAdSize(adaptiveSize());
        mContainer.removeAllViews();
        mContainer.addView(mAdView);
        mAdView.loadAd(new AdRequest.Builder().build());
    }

    private AdSize adaptiveSize()
    {
        DisplayMetrics metrics = mActivity.getResources().getDisplayMetrics();
        int widthDp = (int) (metrics.widthPixels / metrics.density);
        return AdSize.getCurrentOrientationAnchoredAdaptiveBannerAdSize(mActivity, widthDp);
    }

    private void removeBanner()
    {
        if (mAdView != null)
        {
            mContainer.removeView(mAdView);
            mAdView.destroy();
            mAdView = null;
        }
    }
}
