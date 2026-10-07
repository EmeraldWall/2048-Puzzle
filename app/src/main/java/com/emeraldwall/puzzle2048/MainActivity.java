package com.emeraldwall.puzzle2048;

import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.view.KeyEvent;
import android.widget.FrameLayout;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.emeraldwall.puzzle2048.ads.AdBannerController;
import com.emeraldwall.puzzle2048.modes.DailyChallenge;
import com.emeraldwall.puzzle2048.modes.GameMode;

public class MainActivity extends AppCompatActivity implements MainGame.RunListener
{
    public static int mRewardDeletes = 2;

    // delete selection:
    public static int mRewardDeletingSelectionAmounts = 3;

    private static final String REWARD_DELETES = "reward chances";
    private static final String WIDTH = "width";
    private static final String HEIGHT = "height";
    private static final String SCORE = "score";
    private static final String HIGH_SCORE = "high score temp";
    private static final String UNDO_SCORE = "undo score";
    private static final String CAN_UNDO = "can undo";
    private static final String UNDO_GRID = "undo";
    private static final String GAME_STATE = "game state";
    private static final String UNDO_GAME_STATE = "undo game state";
    private static final String REWARD_DELETE_SELECTION = "reward delete selection amounts";

    private MainView view;
    private AdBannerController mAdBanner;
    private GameMode mMode;

    @Override
    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_game);

        FrameLayout frameLayout = findViewById(R.id.game_frame_layout);
        mMode = MainMenuActivity.getMode();
        view = new MainView(this, this);
        view.game.runListener = this;

        SharedPreferences settings = PreferenceManager.getDefaultSharedPreferences(this);
        view.hasSaveState = settings.getBoolean("save_state", false);

        if (savedInstanceState != null && mMode.isClassic())
            if (savedInstanceState.getBoolean("hasState"))
                load();

        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        view.setLayoutParams(params);

        frameLayout.addView(view);

        mAdBanner = new AdBannerController(this, (FrameLayout) findViewById(R.id.ad_container));
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event)
    {
        if (keyCode == KeyEvent.KEYCODE_MENU)
            return true;
        else if (keyCode == KeyEvent.KEYCODE_DPAD_DOWN)
        {
            view.game.move(2);
            return true;
        }
        else if (keyCode == KeyEvent.KEYCODE_DPAD_UP)
        {
            view.game.move(0);
            return true;
        }
        else if (keyCode == KeyEvent.KEYCODE_DPAD_LEFT)
        {
            view.game.move(3);
            return true;
        }
        else if (keyCode == KeyEvent.KEYCODE_DPAD_RIGHT)
        {
            view.game.move(1);
            return true;
        }
        return super.onKeyDown(keyCode, event);
    }

    @Override
    public void onSaveInstanceState(Bundle savedInstanceState)
    {
        if (mMode.isClassic())
        {
            savedInstanceState.putBoolean("hasState", true);
            save();
        }
        super.onSaveInstanceState(savedInstanceState);
    }

    @Override
    protected void onPause()
    {
        mAdBanner.pause();
        super.onPause();
        if (mMode.isClassic())
            save();
        else
            view.game.saveDailyProgress();
    }

    @Override
    protected void onResume()
    {
        super.onResume();
        if (mMode.isClassic())
            load();
        mAdBanner.resume();
    }

    @Override
    protected void onDestroy()
    {
        mAdBanner.destroy();
        super.onDestroy();
    }

    // Results of Daily, Time Attack and Sprint runs
    @Override
    public void onRunFinished(final MainGame.RunResult result)
    {
        if (isFinishing())
            return;

        new AlertDialog.Builder(this)
                .setTitle(resultTitle(result))
                .setMessage(resultMessage(result))
                .setCancelable(false)
                .setPositiveButton(R.string.play_again, new DialogInterface.OnClickListener()
                {
                    @Override
                    public void onClick(DialogInterface dialog, int which)
                    {
                        view.game.newGame();
                    }
                })
                .setNeutralButton(R.string.share_result, new DialogInterface.OnClickListener()
                {
                    @Override
                    public void onClick(DialogInterface dialog, int which)
                    {
                        shareResult(result);
                    }
                })
                .setNegativeButton(R.string.back_to_menu, new DialogInterface.OnClickListener()
                {
                    @Override
                    public void onClick(DialogInterface dialog, int which)
                    {
                        finish();
                    }
                })
                .show();
    }

    private String resultTitle(MainGame.RunResult r)
    {
        switch (r.mode)
        {
            case DAILY:
                return r.goalMet ? "Daily goal complete!" : "Daily Challenge " + DailyChallenge.label(r.day);
            case TIME_ATTACK:
                return r.newBest ? "New best! Time's up" : "Time's up";
            default:
                return r.won ? (r.newBest ? "New best time!" : "512 reached") : "Board locked";
        }
    }

    private String resultMessage(MainGame.RunResult r)
    {
        StringBuilder text = new StringBuilder();
        if (r.mode == GameMode.SPRINT && r.won)
            text.append("Time: ").append(formatTime(r.elapsedMs)).append("\n");
        text.append("Score: ").append(r.score).append("\nMoves: ").append(r.moves);

        if (r.mode == GameMode.DAILY)
        {
            text.append("\nBest today: ").append(Math.max(r.bestScoreToday, r.score));
            text.append("\nGoal: ").append(r.goalMet ? "complete" : "not reached");
            text.append("\nStreak: ").append(r.streak).append(r.streak == 1 ? " day" : " days");
            text.append("\n\nSame puzzle for everyone today. Retry to beat your score, and come back tomorrow for a new one.");
        }
        else if (r.newBest && r.mode == GameMode.TIME_ATTACK)
            text.append("\nA new personal best.");
        return text.toString();
    }

    private void shareResult(MainGame.RunResult r)
    {
        String line;
        switch (r.mode)
        {
            case DAILY:
                line = "2048 Daily " + DailyChallenge.label(r.day) + ": " + r.score + " points"
                        + (r.goalMet ? ", goal complete" : "") + ". Streak " + r.streak + ".";
                break;
            case TIME_ATTACK:
                line = "2048 Time Attack: " + r.score + " points in a minute and change.";
                break;
            default:
                line = r.won ? "2048 Sprint: 512 in " + formatTime(r.elapsedMs) + "." : "2048 Sprint: tough board!";
                break;
        }

        Intent send = new Intent(Intent.ACTION_SEND);
        send.setType("text/plain");
        send.putExtra(Intent.EXTRA_TEXT, line + " Can you beat me?\nhttps://play.google.com/store/apps/details?id=" + getPackageName());
        startActivity(Intent.createChooser(send, getString(R.string.share_title)));
    }

    private static String formatTime(long ms)
    {
        long seconds = ms / 1000;
        return (seconds / 60) + ":" + (seconds % 60 < 10 ? "0" : "") + (seconds % 60) + "." + (ms % 1000) / 100;
    }

    private void save()
    {
        final int rows = MainMenuActivity.getRows();

        SharedPreferences settings = PreferenceManager.getDefaultSharedPreferences(this);
        SharedPreferences.Editor editor = settings.edit();
        Tile[][] field = view.game.grid.field;
        Tile[][] undoField = view.game.grid.undoField;
        editor.putInt(WIDTH + rows, field.length);
        editor.putInt(HEIGHT + rows, field.length);

        for (int xx = 0; xx < field.length; xx++)
        {
            for (int yy = 0; yy < field[0].length; yy++)
            {
                if (field[xx][yy] != null)
                    editor.putInt(rows + " " + xx + " " + yy, field[xx][yy].getValue());
                else
                    editor.putInt(rows + " " + xx + " " + yy, 0);

                if (undoField[xx][yy] != null)
                    editor.putInt(UNDO_GRID + rows + " " + xx + " " + yy, undoField[xx][yy].getValue());
                else
                    editor.putInt(UNDO_GRID + rows + " " + xx + " " + yy, 0);
            }
        }

        // reward deletions:
        editor.putInt(REWARD_DELETES + rows, mRewardDeletes);
        editor.putInt(REWARD_DELETE_SELECTION + rows, mRewardDeletingSelectionAmounts);

        // game values:
        editor.putLong(SCORE + rows, view.game.score);
        editor.putLong(HIGH_SCORE + rows, view.game.highScore);
        editor.putLong(UNDO_SCORE + rows, view.game.lastScore);
        editor.putBoolean(CAN_UNDO + rows, view.game.canUndo);
        editor.putInt(GAME_STATE + rows, view.game.gameState);
        editor.putInt(UNDO_GAME_STATE + rows, view.game.lastGameState);
        editor.apply();
    }

    private void load()
    {
        final int rows = MainMenuActivity.getRows();

        //Stopping all animations
        view.game.aGrid.cancelAnimations();

        SharedPreferences settings = PreferenceManager.getDefaultSharedPreferences(this);

        for (int xx = 0; xx < view.game.grid.field.length; xx++)
        {
            for (int yy = 0; yy < view.game.grid.field[0].length; yy++)
            {
                int value = settings.getInt( rows + " " + xx + " " + yy, -1);
                if (value > 0)
                    view.game.grid.field[xx][yy] = new Tile(xx, yy, value);
                else if (value == 0)
                    view.game.grid.field[xx][yy] = null;

                int undoValue = settings.getInt(UNDO_GRID + rows + " " + xx + " " + yy, -1);
                if (undoValue > 0)
                    view.game.grid.undoField[xx][yy] = new Tile(xx, yy, undoValue);
                else if (value == 0)
                    view.game.grid.undoField[xx][yy] = null;
            }
        }

        mRewardDeletes = settings.getInt(REWARD_DELETES + rows, 2);
        mRewardDeletingSelectionAmounts = settings.getInt(REWARD_DELETE_SELECTION + rows, 3);

        view.game.score = settings.getLong(SCORE + rows, view.game.score);
        view.game.highScore = settings.getLong(HIGH_SCORE + rows, view.game.highScore);
        view.game.lastScore = settings.getLong(UNDO_SCORE + rows, view.game.lastScore);
        view.game.canUndo = settings.getBoolean(CAN_UNDO + rows, view.game.canUndo);
        view.game.gameState = settings.getInt(GAME_STATE + rows, view.game.gameState);
        view.game.lastGameState = settings.getInt(UNDO_GAME_STATE + rows, view.game.lastGameState);
    }
}
