using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Plugin.Firebase.CloudMessaging;

namespace MetarPulse.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 13+ için bildirim izni runtime'da istenmeli
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.PostNotifications)
                != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this,
                    [Android.Manifest.Permission.PostNotifications], 0);
            }
        }

        // FCM notification channel oluştur
        CreateNotificationChannel();

        // FCM: uygulama bildirime tıklanarak açılırsa intent'i ilet
        try { FirebaseCloudMessagingImplementation.OnNewIntent(Intent); } catch { }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        try { FirebaseCloudMessagingImplementation.OnNewIntent(intent); } catch { }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        // FcmService.cs'deki ChannelId ile aynı olmalı — eşleşmezse bildirim sessizce düşer
        const string channelId = "metar_alerts";
        var notificationManager = (NotificationManager?) GetSystemService(NotificationService);

        var channel = new NotificationChannel(
            channelId,
            "METAR Bildirimleri",
            NotificationImportance.High)
        {
            Description = "Yeni METAR ve uçuş koşulu değişiklik bildirimleri"
        };

        notificationManager?.CreateNotificationChannel(channel);

        // Plugin'e channel ID'yi bildir
        try { FirebaseCloudMessagingImplementation.ChannelId = channelId; } catch { }
    }
}
