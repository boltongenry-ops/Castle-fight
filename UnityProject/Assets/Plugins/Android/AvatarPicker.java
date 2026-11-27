package com.castlefight.profile;

import android.app.Activity;
import android.app.Fragment;
import android.content.ContentResolver;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.net.Uri;
import android.os.Bundle;
import android.util.Base64;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;

public final class AvatarPicker extends Fragment {
    public interface Callback { void complete(String image, String error); }
    private static final int REQUEST = 4914;
    private Callback callback;

    public static void pick(Activity activity, Callback receiver) {
        activity.runOnUiThread(() -> {
            try {
                AvatarPicker picker = new AvatarPicker();
                picker.callback = receiver;
                activity.getFragmentManager().beginTransaction().add(picker, "CastleFightAvatar").commit();
            } catch (RuntimeException failure) {
                receiver.complete("", "Не удалось открыть выбор изображения.");
            }
        });
    }

    @Override public void onActivityCreated(Bundle saved) {
        super.onActivityCreated(saved);
        if (saved != null || callback == null) { remove(); return; }
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.setType("image/*");
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        try { startActivityForResult(intent, REQUEST); }
        catch (RuntimeException failure) { callback.complete("", "Не удалось открыть выбор изображения."); remove(); }
    }

    @Override public void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST) return;
        Callback receiver = callback;
        Uri uri = data == null ? null : data.getData();
        ContentResolver resolver = getActivity().getContentResolver();
        remove();
        if (receiver == null) return;
        if (result != Activity.RESULT_OK || uri == null) { receiver.complete("", ""); return; }
        new Thread(() -> {
            try { receiver.complete(prepare(resolver, uri), ""); }
            catch (Exception failure) { receiver.complete("", "Не удалось прочитать изображение."); }
        }, "CastleFightAvatar").start();
    }

    private void remove() {
        if (isAdded()) getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
    }

    private static String prepare(ContentResolver resolver, Uri uri) throws IOException {
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inJustDecodeBounds = true;
        try (InputStream input = resolver.openInputStream(uri)) {
            BitmapFactory.decodeStream(input, null, options);
        }
        if (options.outWidth < 1 || options.outHeight < 1 || options.outWidth > 12000 || options.outHeight > 12000)
            throw new IOException("Invalid image bounds");
        int longest = Math.max(options.outWidth, options.outHeight);
        options.inJustDecodeBounds = false;
        options.inSampleSize = 1;
        while (longest / options.inSampleSize > 512) options.inSampleSize *= 2;
        Bitmap original;
        try (InputStream input = resolver.openInputStream(uri)) {
            original = BitmapFactory.decodeStream(input, null, options);
        }
        if (original == null) throw new IOException("Invalid image");
        Bitmap square = Bitmap.createBitmap(128, 128, Bitmap.Config.ARGB_8888);
        try {
            int side = Math.min(original.getWidth(), original.getHeight());
            int x = (original.getWidth() - side) / 2, y = (original.getHeight() - side) / 2;
            Canvas canvas = new Canvas(square);
            canvas.drawBitmap(original, new Rect(x,y,x+side,y+side), new Rect(0,0,128,128), new Paint(Paint.FILTER_BITMAP_FLAG));
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            for (int quality = 90; quality >= 40; quality -= 10) {
                bytes.reset();
                if (square.compress(Bitmap.CompressFormat.JPEG, quality, bytes) && bytes.size() <= 16384)
                    return Base64.encodeToString(bytes.toByteArray(), Base64.NO_WRAP);
            }
            throw new IOException("Image cannot be compressed");
        } finally {
            square.recycle();
            original.recycle();
        }
    }
}
