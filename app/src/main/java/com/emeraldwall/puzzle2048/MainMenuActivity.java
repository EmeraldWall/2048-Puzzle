package com.emeraldwall.puzzle2048;

import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.view.MenuInflater;
import android.view.MenuItem;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.PopupMenu;

import com.emeraldwall.puzzle2048.ads.AdBannerController;
import com.emeraldwall.puzzle2048.ads.AdFreeStore;
import com.emeraldwall.puzzle2048.ads.BillingManager;

public class MainMenuActivity extends AppCompatActivity
        implements PopupMenu.OnMenuItemClickListener, BillingManager.Listener
{
    public static boolean mIsMainMenu = true;

    private static int mRows = 4;
    public static int getRows() { return mRows; }

    private static final String BACKGROUND_COLOR_KEY = "BackgroundColor";
    public static int mBackgroundColor = 0;

    private AdBannerController mAdBanner;
    private BillingManager mBilling;
    private Button mRemoveAdsButton;
    private boolean mAdsWereRemoved;

    @Override
    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main_menu);

        mIsMainMenu = true;

        Typeface font = Typeface.createFromAsset(getAssets(), "ClearSans-Bold.ttf");
        ((Button) findViewById(R.id.btn_start_4x4)).setTypeface(font);
        ((Button) findViewById(R.id.btn_start_5x5)).setTypeface(font);
        ((Button) findViewById(R.id.btn_start_6x6)).setTypeface(font);

        mRemoveAdsButton = findViewById(R.id.btn_remove_ads);
        mRemoveAdsButton.setTypeface(font);
        mAdsWereRemoved = AdFreeStore.isAdsRemoved(this);
        updateRemoveAdsButton(mAdsWereRemoved);

        mAdBanner = new AdBannerController(this, (FrameLayout) findViewById(R.id.ad_container));
        mBilling = new BillingManager(this, this);
    }

    @Override
    protected void onResume()
    {
        super.onResume();
        mIsMainMenu = true;
        mAdBanner.resume();

        SaveColors();
        LoadColors();
    }

    @Override
    protected void onPause()
    {
        mAdBanner.pause();
        super.onPause();
    }

    @Override
    protected void onDestroy()
    {
        mAdBanner.destroy();
        mBilling.destroy();
        super.onDestroy();
    }

    // Buttons:
    public void onButtonsClick(View view)
    {
        int id = view.getId();

        if (id == R.id.btn_start_4x4)
            StartGame(4);
        else if (id == R.id.btn_start_5x5)
            StartGame(5);
        else if (id == R.id.btn_start_6x6)
            StartGame(6);
        else if (id == R.id.btn_remove_ads)
            mBilling.buy(this);
        else if (id == R.id.btn_settings)
            showSettingsMenu(view);
        else if (id == R.id.btn_share)
            shareGame();
        else if (id == R.id.btn_rate)
            rateGame();
        else if (id == R.id.btn_send_email)
            contactSupport();
    }

    private void showSettingsMenu(View anchor)
    {
        PopupMenu popup = new PopupMenu(this, anchor);
        popup.setOnMenuItemClickListener(this);
        MenuInflater inflater = popup.getMenuInflater();
        inflater.inflate(R.menu.menus, popup.getMenu());
        popup.getMenu().findItem(R.id.settings_remove_ads)
                .setVisible(!AdFreeStore.isAdsRemoved(this));
        popup.show();
    }

    private void shareGame()
    {
        Intent shareIntent = new Intent(Intent.ACTION_SEND);
        shareIntent.setType("text/plain");
        shareIntent.putExtra(Intent.EXTRA_TEXT, getString(R.string.share_text) + "\n\n" + storeUrl());
        startActivity(Intent.createChooser(shareIntent, getString(R.string.share_title)));
    }

    private void rateGame()
    {
        try
        {
            startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + getPackageName())));
        }
        catch (ActivityNotFoundException e)
        {
            startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(storeUrl())));
        }
    }

    private void contactSupport()
    {
        Intent emailIntent = new Intent(Intent.ACTION_SENDTO, Uri.parse("mailto:"));
        emailIntent.putExtra(Intent.EXTRA_EMAIL, new String[]{getString(R.string.support_email)});
        emailIntent.putExtra(Intent.EXTRA_SUBJECT, getString(R.string.email_subject));
        try
        {
            startActivity(Intent.createChooser(emailIntent, getString(R.string.email_send_title)));
        }
        catch (ActivityNotFoundException e)
        {
            Toast.makeText(this, R.string.email_client_error, Toast.LENGTH_SHORT).show();
        }
    }

    @Override
    public boolean onMenuItemClick(MenuItem item)
    {
        int id = item.getItemId();
        if (id == R.id.settings_color_picker)
        {
            mRows = 4;  // because of its GameView!
            startActivity(new Intent(this, ColorPickerActivity.class));
        }
        else if (id == R.id.settings_remove_ads)
            mBilling.buy(this);
        else if (id == R.id.settings_restore_purchases)
            mBilling.restore();
        return false;
    }

    // BillingManager.Listener
    @Override
    public void onAdsRemovedChanged(boolean removed)
    {
        updateRemoveAdsButton(removed);
        mAdBanner.resume();
        if (removed && !mAdsWereRemoved)
            Toast.makeText(this, R.string.ads_removed_thanks, Toast.LENGTH_LONG).show();
        mAdsWereRemoved = removed;
    }

    @Override
    public void onPriceLoaded(String formattedPrice)
    {
        if (!AdFreeStore.isAdsRemoved(this))
            mRemoveAdsButton.setText(getString(R.string.remove_ads_with_price, formattedPrice));
    }

    @Override
    public void onMessage(int stringResId)
    {
        Toast.makeText(this, stringResId, Toast.LENGTH_LONG).show();
    }

    private void updateRemoveAdsButton(boolean removed)
    {
        mRemoveAdsButton.setVisibility(removed ? View.GONE : View.VISIBLE);
    }

    private String storeUrl()
    {
        return "https://play.google.com/store/apps/details?id=" + getPackageName();
    }

    private void SaveColors()
    {
        SharedPreferences settings = PreferenceManager.getDefaultSharedPreferences(this);
        SharedPreferences.Editor editor = settings.edit();

        if(mBackgroundColor < 0)
            editor.putInt(BACKGROUND_COLOR_KEY, mBackgroundColor);

        editor.apply();
    }

    private void LoadColors()
    {
        SharedPreferences settings = PreferenceManager.getDefaultSharedPreferences(this);

        if(settings.getInt(BACKGROUND_COLOR_KEY, mBackgroundColor) < 0)
            mBackgroundColor = settings.getInt(BACKGROUND_COLOR_KEY, mBackgroundColor);
        else
            mBackgroundColor = getResources().getColor(R.color.colorBackground);
    }

    private void StartGame(int rows)
    {
        mRows = rows;
        mIsMainMenu = false;
        startActivity(new Intent(this, MainActivity.class));
    }
}
