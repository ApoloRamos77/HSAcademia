package com.adhsoftsport.app.reminders

import android.app.AlarmManager
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import org.json.JSONArray
import org.json.JSONObject

/**
 * Recordatorios locales de pago (sin servidor de notificaciones):
 * la app calcula las fechas a partir de las deudas y aquí se programan con AlarmManager.
 * La lista se guarda para volver a programarla si el teléfono se reinicia.
 */
object ReminderScheduler {
  const val CHANNEL_ID = "pagos"
  private const val PREFS = "clubio_reminders"
  private const val KEY = "items"

  data class Item(val id: Int, val title: String, val body: String, val at: Long)

  fun ensureChannel(context: Context) {
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
      val nm = context.getSystemService(NotificationManager::class.java)
      if (nm.getNotificationChannel(CHANNEL_ID) == null) {
        val channel = NotificationChannel(CHANNEL_ID, "Pagos y vencimientos", NotificationManager.IMPORTANCE_DEFAULT)
        channel.description = "Recordatorios de mensualidades por vencer y vencidas"
        nm.createNotificationChannel(channel)
      }
    }
  }

  /** Reemplaza todos los recordatorios por los nuevos (los ya pasados se omiten). */
  fun replaceAll(context: Context, items: List<Item>) {
    cancelAll(context)
    val now = System.currentTimeMillis()
    val future = items.filter { it.at > now }
    val json = JSONArray()
    future.forEach {
      json.put(JSONObject().put("id", it.id).put("title", it.title).put("body", it.body).put("at", it.at))
      schedule(context, it)
    }
    prefs(context).edit().putString(KEY, json.toString()).apply()
  }

  fun cancelAll(context: Context) {
    val alarm = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
    stored(context).forEach { alarm.cancel(pendingIntent(context, it)) }
    prefs(context).edit().remove(KEY).apply()
  }

  /** Tras reiniciar el teléfono: vuelve a programar los recordatorios futuros. */
  fun rescheduleStored(context: Context) {
    val now = System.currentTimeMillis()
    stored(context).filter { it.at > now }.forEach { schedule(context, it) }
  }

  fun stored(context: Context): List<Item> {
    val raw = prefs(context).getString(KEY, null) ?: return emptyList()
    return try {
      val arr = JSONArray(raw)
      (0 until arr.length()).map {
        val o = arr.getJSONObject(it)
        Item(o.getInt("id"), o.getString("title"), o.getString("body"), o.getLong("at"))
      }
    } catch (e: Exception) {
      emptyList()
    }
  }

  private fun schedule(context: Context, item: Item) {
    val alarm = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
    // Alarma inexacta permitida en reposo: no requiere el permiso de alarmas exactas.
    alarm.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, item.at, pendingIntent(context, item))
  }

  private fun pendingIntent(context: Context, item: Item): PendingIntent {
    val intent = Intent(context, ReminderReceiver::class.java)
      .putExtra("id", item.id)
      .putExtra("title", item.title)
      .putExtra("body", item.body)
    return PendingIntent.getBroadcast(
      context, item.id, intent,
      PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
    )
  }

  private fun prefs(context: Context) = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
}
