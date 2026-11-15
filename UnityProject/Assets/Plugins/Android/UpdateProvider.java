package com.castlefight.updates;
import android.app.Activity;
import android.content.*;
import android.content.pm.*;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.*;
import android.provider.OpenableColumns;
import java.io.*;
import java.util.Arrays;
public final class UpdateProvider extends ContentProvider {
    private static File apk(Context c){return new File(c.getCacheDir(),"castle-fight-update.apk");}
    public static String downloadPath(Context c){return apk(c).getAbsolutePath();}
    public static boolean canInstall(Context c){return Build.VERSION.SDK_INT<26||c.getPackageManager().canRequestPackageInstalls();}
    private static Signature[] signatures(PackageInfo info){return Build.VERSION.SDK_INT>=28?info.signingInfo.getApkContentsSigners():info.signatures;}
    public static String validate(Context c,String path){try{
        File file=new File(path);if(!file.getCanonicalPath().equals(apk(c).getCanonicalPath()))return "Неверный путь обновления.";
        PackageManager pm=c.getPackageManager();int flags=Build.VERSION.SDK_INT>=28?PackageManager.GET_SIGNING_CERTIFICATES:PackageManager.GET_SIGNATURES;
        PackageInfo incoming=pm.getPackageArchiveInfo(path,flags),installed=pm.getPackageInfo(c.getPackageName(),flags);
        if(incoming==null||!c.getPackageName().equals(incoming.packageName))return "APK относится к другому приложению.";
        long next=Build.VERSION.SDK_INT>=28?incoming.getLongVersionCode():incoming.versionCode,old=Build.VERSION.SDK_INT>=28?installed.getLongVersionCode():installed.versionCode;
        if(next<=old)return "Эта версия уже установлена.";
        Signature[] a=signatures(incoming),b=signatures(installed);if(a==null||b==null||a.length!=b.length)return "Подпись обновления не совпадает.";
        for(int i=0;i<a.length;i++)if(!Arrays.equals(a[i].toByteArray(),b[i].toByteArray()))return "Подпись обновления не совпадает.";
        return "";
    }catch(Exception e){return "Не удалось проверить подпись обновления.";}}
    public static void install(Activity activity){
        if(!canInstall(activity)){activity.startActivity(new Intent("android.settings.MANAGE_UNKNOWN_APP_SOURCES",Uri.parse("package:"+activity.getPackageName())));return;}
        Uri uri=new Uri.Builder().scheme("content").authority(activity.getPackageName()+".updates").appendPath("castle-fight-update.apk").build();
        Intent intent=new Intent(Intent.ACTION_VIEW).setDataAndType(uri,"application/vnd.android.package-archive").addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.startActivity(intent);
    }
    private File resolve(Uri uri) throws FileNotFoundException {try{
        if(!"/castle-fight-update.apk".equals(uri.getPath()))throw new FileNotFoundException();File file=apk(getContext());if(!file.isFile())throw new FileNotFoundException();return file;
    }catch(Exception e){throw new FileNotFoundException("Update unavailable");}}
    public boolean onCreate(){return true;}
    public String getType(Uri uri){return "application/vnd.android.package-archive";}
    public ParcelFileDescriptor openFile(Uri uri,String mode) throws FileNotFoundException {if(!"r".equals(mode))throw new FileNotFoundException("Read only");return ParcelFileDescriptor.open(resolve(uri),ParcelFileDescriptor.MODE_READ_ONLY);}
    public Cursor query(Uri uri,String[] projection,String selection,String[] args,String sort){try{File file=resolve(uri);String[] columns=projection!=null?projection:new String[]{OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE};MatrixCursor result=new MatrixCursor(columns);Object[] row=new Object[columns.length];for(int i=0;i<columns.length;i++)row[i]=OpenableColumns.SIZE.equals(columns[i])?file.length():OpenableColumns.DISPLAY_NAME.equals(columns[i])?file.getName():null;result.addRow(row);return result;}catch(Exception e){return null;}}
    public Uri insert(Uri uri,ContentValues v){throw new UnsupportedOperationException();}
    public int update(Uri uri,ContentValues v,String s,String[] a){throw new UnsupportedOperationException();}
    public int delete(Uri uri,String s,String[] a){throw new UnsupportedOperationException();}
}
