package com.emeraldwall.puzzle2048;

import android.Manifest;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Typeface;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.view.MenuInflater;
import android.view.MenuItem;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.PopupMenu;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.emeraldwall.puzzle2048.ads.AdBannerController;
import com.emeraldwall.puzzle2048.ads.AdFreeStore;
import com.emeraldwall.puzzle2048.ads.BillingManager;
import com.emeraldwall.puzzle2048.modes.DailyChallenge;
import com.emeraldwall.puzzle2048.modes.GameMode;
import com.emeraldwall.puzzle2048.modes.ProgressStore;
import com.emeraldwall.puzzle2048.reminder.ReminderScheduler;

import java.text.NumberFormat;
import java.util.Locale;

public class MainMenuActivity extends AppCompatActivity
        implements PopupMenu.OnMenuItemClickListener, BillingManager.Listener
{
    public static boolean mIsMainMenu = true;

    private static int mRows = 4;
    public static int getRows() { return mRows; }

    private static GameMode mMode = GameMode.CLASSIC;
    public static GameMode getMode() { return mMode; }

    private static final int REQUEST_NOTIFICATIONS = 31;

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
        refreshHome();
        maybeOfferReminder();
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
        else if (id == R.id.btn_daily)
            startMode(GameMode.DAILY, DailyChallenge.goalFor(DailyChallenge.dayNumber()).rows);
        else if (id == R.id.btn_time_attack)
            startMode(GameMode.TIME_ATTACK, 4);
        else if (id == R.id.btn_sprint)
            startMode(GameMode.SPRINT, 4);
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
        popup.getMenu().findItem(R.id.settings_daily_reminder).setTitle(
                ProgressStore.isReminderEnabled(this) ? R.string.daily_reminder_on : R.string.daily_reminder_off);
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
            mMode = GameMode.CLASSIC;
            startActivity(new Intent(this, ColorPickerActivity.class));
        }
        else if (id == R.id.settings_remove_ads)
            mBilling.buy(this);
        else if (id == R.id.settings_restore_purchases)
            mBilling.restore();
        else if (id == R.id.settings_daily_reminder)
            setReminder(!ProgressStore.isReminderEnabled(this));
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
        startMode(GameMode.CLASSIC, rows);
    }

    private void startMode(GameMode mode, int rows)
    {
        mMode = mode;
        mRows = rows;
        mIsMainMenu = false;
        startActivity(new Intent(this, MainActivity.class));
    }

    // Level, daily card and mode records
    private void refreshHome()
    {
        NumberFormat number = NumberFormat.getIntegerInstance(Locale.US);
        int today = DailyChallenge.dayNumber();

        long points = ProgressStore.totalPoints(this);
        int level = ProgressStore.levelFor(points);
        long start = ProgressStore.levelStart(level);
        long next = ProgressStore.levelStart(level + 1);
        ((TextView) findViewById(R.id.level_text)).setText("Level " + level);
        ((ProgressBar) findViewById(R.id.level_bar)).setProgress((int) (100 * (points - start) / (next - start)));
        ((TextView) findViewById(R.id.level_sub)).setText(
                number.format(next - points) + " points to level " + (level + 1));

        DailyChallenge.Goal goal = DailyChallenge.goalFor(today);
        int streak = ProgressStore.currentStreak(this, today);
        String streakText = streak > 0 ? "Streak: " + streak + (streak == 1 ? " day" : " days") : "No streak yet";
        String status;
        if (ProgressStore.hasPlayedDaily(this, today))
        {
            status = (ProgressStore.isGoalMet(this, today) ? "Goal complete. " : "Goal not reached yet. ")
                    + "Best today: " + number.format(ProgressStore.dailyBestScore(this, today));
        }
        else
            status = streak > 0 ? "Play today to keep your streak" : "Play today to start a streak";

        ((Button) findViewById(R.id.btn_daily)).setText(
                "DAILY CHALLENGE  " + DailyChallenge.label(today) + "\n"
                + goal.title() + " on " + goal.rows + "x" + goal.rows + "\n"
                + status + "\n" + streakText);

        long taBest = MainGame.readHighScore(this, 4, GameMode.TIME_ATTACK);
        ((Button) findViewById(R.id.btn_time_attack)).setText("TIME ATTACK\n60 seconds\nBest: "
                + (taBest > 0 ? number.format(taBest) : "none"));

        long sprintBest = ProgressStore.sprintBestMs(this);
        ((Button) findViewById(R.id.btn_sprint)).setText("SPRINT\nReach 512 fast\nBest: "
                + (sprintBest > 0 ? formatSprint(sprintBest) : "none"));
    }

    private static String formatSprint(long ms)
    {
        long seconds = ms / 1000;
        return (seconds / 60) + ":" + (seconds % 60 < 10 ? "0" : "") + (seconds % 60) + "." + (ms % 1000) / 100;
    }

    // Daily reminder
    private void setReminder(boolean enabled)
    {
        if (enabled)
        {
            ReminderScheduler.enable(this);
            if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(this,
                    Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
                ActivityCompat.requestPermissions(this,
                        new String[]{Manifest.permission.POST_NOTIFICATIONS}, REQUEST_NOTIFICATIONS);
            Toast.makeText(this, R.string.daily_reminder_enabled, Toast.LENGTH_SHORT).show();
        }
        else
            ReminderScheduler.disable(this);
    }

    // Asked once, right after the player has tried their first daily challenge
    private void maybeOfferReminder()
    {
        if (ProgressStore.wasReminderPrompted(this) || !ProgressStore.everPlayedDaily(this)
                || ProgressStore.isReminderEnabled(this))
            return;

        ProgressStore.setReminderPrompted(this);
        new AlertDialog.Builder(this)
                .setTitle(R.string.reminder_prompt_title)
                .setMessage(R.string.reminder_prompt_message)
                .setPositiveButton(R.string.reminder_prompt_yes, new android.content.DialogInterface.OnClickListener()
                {
                    @Override
                    public void onClick(android.content.DialogInterface dialog, int which)
                    {
                        setReminder(true);
                    }
                })
                .setNegativeButton(R.string.reminder_prompt_no, null)
                .show();
    }
}
