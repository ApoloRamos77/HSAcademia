package com.adhsoftsport.app.reminders

import androidx.core.app.NotificationManagerCompat
import com.facebook.react.bridge.Promise
import com.facebook.react.bridge.ReactApplicationContext
import com.facebook.react.bridge.ReactContextBaseJavaModule
import com.facebook.react.bridge.ReactMethod
import com.facebook.react.bridge.ReadableArray

/** Puente JS → Android: NativeModules.ClubioReminders. */
class RemindersModule(private val ctx: ReactApplicationContext) : ReactContextBaseJavaModule(ctx) {
  override fun getName() = "ClubioReminders"

  /** items: [{ id: number, title: string, body: string, at: number (ms epoch) }] */
  @ReactMethod
  fun replaceAll(items: ReadableArray, promise: Promise) {
    try {
      ReminderScheduler.ensureChannel(ctx)
      val list = ArrayList<ReminderScheduler.Item>()
      for (i in 0 until items.size()) {
        val m = items.getMap(i) ?: continue
        list.add(ReminderScheduler.Item(m.getInt("id"), m.getString("title") ?: "", m.getString("body") ?: "", m.getDouble("at").toLong()))
      }
      ReminderScheduler.replaceAll(ctx, list)
      promise.resolve(ReminderScheduler.stored(ctx).size)
    } catch (e: Exception) {
      promise.reject("E_REMINDERS", e.message, e)
    }
  }

  @ReactMethod
  fun cancelAll(promise: Promise) {
    ReminderScheduler.cancelAll(ctx)
    promise.resolve(null)
  }

  @ReactMethod
  fun notificationsEnabled(promise: Promise) {
    promise.resolve(NotificationManagerCompat.from(ctx).areNotificationsEnabled())
  }
}
