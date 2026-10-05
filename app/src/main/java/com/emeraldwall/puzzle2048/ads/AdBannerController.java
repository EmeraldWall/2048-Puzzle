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
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.UserMessagingPlatform;

import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Owns the permanent bottom banner of one screen. Collapses the container completely
 * when the user has purchased "remove ads", and loads nothing until consent allows it.
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
            gatherConsentThenLoad();
    }

    private void gatherConsentThenLoad()
    {
        final ConsentInformation consent = UserMessagingPlatform.getConsentInformation(mActivity);

        // Consent from an earlier session may already allow ads.
        if (consent.canRequestAds())
            loadBanner();

        consent.requestConsentInfoUpdate(mActivity, new ConsentRequestParameters.Builder().build(),
                () -> UserMessagingPlatform.loadAndShowConsentFormIfRequired(mActivity, formError ->
                {
                    if (consent.canRequestAds())
                        loadBanner();
                }),
                formError -> { /* keep whatever consent state we already have */ });
    }

    private void loadBanner()
    {
        if (mDestroyed || mAdView != null || mActivity.isFinishing())
            return;
        if (AdFreeStore.isAdsRemoved(mActivity))
            return;

        if (sMobileAdsStarted.compareAndSet(false, true))
            MobileAds.initialize(mActivity.getApplicationContext());

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
