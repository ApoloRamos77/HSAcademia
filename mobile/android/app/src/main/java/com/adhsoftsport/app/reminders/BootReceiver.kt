package com.adhsoftsport.app.reminders

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/** Android borra las alarmas al reiniciar o actualizar la app: se vuelven a programar desde lo guardado. */
class BootReceiver : BroadcastReceiver() {
  override fun onReceive(context: Context, intent: Intent) {
    if (intent.action == Intent.ACTION_BOOT_COMPLETED || intent.action == Intent.ACTION_MY_PACKAGE_REPLACED) {
      ReminderScheduler.rescheduleStored(context)
    }
  }
}
