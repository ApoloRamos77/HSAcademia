package com.adhsoftsport.app

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import com.facebook.react.PackageList
import com.facebook.react.ReactApplication
import com.facebook.react.ReactHost
import com.facebook.react.ReactNativeApplicationEntryPoint.loadReactNative
import com.facebook.react.defaults.DefaultReactHost.getDefaultReactHost
import com.adhsoftsport.app.reminders.RemindersPackage

class MainApplication : Application(), ReactApplication {

  override val reactHost: ReactHost by lazy {
    getDefaultReactHost(
      context = applicationContext,
      packageList =
        PackageList(this).packages.apply {
          // Packages that cannot be autolinked yet can be added manually here, for example:
          // add(MyReactNativePackage())
          add(RemindersPackage()) // recordatorios locales de pago
        },
    )
  }

  override fun onCreate() {
    super.onCreate()
    loadReactNative(this)
    createNoticesChannel()
  }

  /** Canal de las notificaciones que envía el servidor (FCM exige que exista). */
  private fun createNoticesChannel() {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
    val channel = NotificationChannel("avisos", "Avisos de la academia", NotificationManager.IMPORTANCE_HIGH)
    getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
  }
}
