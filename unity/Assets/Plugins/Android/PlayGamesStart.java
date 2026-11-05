package com.saturnwave.museumpuzzle;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.net.Uri;
import android.util.Log;

import com.google.android.gms.games.PlayGamesSdk;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

/**
 * Starts Google Play Games with the game's process, as the SDK's own provider does (PlayGamesInitProvider, which the
 * build withdraws: unity/Assets/Editor/ManifestPlayGamesStart.cs), but only while the player's cloud save switch is on:
 * the owner's decision 1, under which nothing of the cloud runs with the switch off (docs/research/server-v1.md 3.8).
 * <p>
 * The SDK signs a player who uses Play Games on this phone in by itself only as an activity is created: its
 * initialisation registers activity callbacks, and the attempt starts in onActivityCreated alone (bytecode of
 * play-services-games-v2 22.1.0, internal.games_v2.zzar). Started after the game's activity is there, the SDK makes no
 * attempt, and the game's quiet sign-in at the start (GamesSignInClient.isAuthenticated) answers with error 10, so a new
 * phone would not bring the player's progress back (docs/LESSONS.md H7). A provider runs before any activity of its
 * process.
 * <p>
 * The switch is read as the game keeps it, in cloud.json beside the progress file (Application.persistentDataPath, which
 * is getExternalFilesDir on Android; Scripts/Cloud/CloudStateFile.cs): on for a new installation, which has no file; the
 * kept "switchedOn" of a file the game wrote; off for a file that does not read, which the game sets aside and starts
 * over with the switch off, so that an "off" never turns into an "on" by accident. The provider serves no data.
 */
public final class PlayGamesStart extends ContentProvider {
    private static final String TAG = "PlayGamesStart";

    /** The game's cloud.json, as CloudStateFile names it. */
    private static final String CLOUD_STATE = "cloud.json";

    @Override
    public boolean onCreate() {
        Context context = getContext();
        if (context == null) {
            Log.w(TAG, "Google Play Games does not start with the game: the provider has no context; the game's first sign-in starts it.");
            return false;
        }

        if (!isTheCloudSwitchOn(context)) {
            Log.i(TAG, "Google Play Games does not start with the game: the player's cloud save is off.");
            return false;
        }

        try {
            PlayGamesSdk.initialize(context);
            Log.i(TAG, "Google Play Games starts with the game: the player's cloud save is on.");
        } catch (RuntimeException refused) {
            // A provider that throws stops the game at every start (docs/LESSONS.md H5). The SDK is closed, so what it
            // throws is written to the log, and the plugin starts the SDK at the game's first sign-in, as it did before.
            Log.w(TAG, "Google Play Games did not start with the game; the game's first sign-in starts it.", refused);
        }

        return false;
    }

    /** The player's cloud save switch as cloud.json keeps it (see the class). */
    private static boolean isTheCloudSwitchOn(Context context) {
        File folder = context.getExternalFilesDir(null);
        File state = new File(folder != null ? folder : context.getFilesDir(), CLOUD_STATE);
        if (!state.exists()) {
            return true;
        }

        try {
            JSONObject kept = new JSONObject(new String(Files.readAllBytes(state.toPath()), StandardCharsets.UTF_8));
            boolean isWrittenByTheGame = kept.optInt("version", 0) >= 1 && !kept.optString("writer", "").isEmpty();
            return isWrittenByTheGame && kept.optBoolean("switchedOn", false);
        } catch (IOException | JSONException unreadable) {
            Log.i(TAG, "cloud.json does not read, so the game starts with the cloud save off: " + unreadable.getMessage());
            return false;
        }
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        return null;
    }

    @Override
    public String getType(Uri uri) {
        return null;
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        return null;
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        return 0;
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        return 0;
    }
}
