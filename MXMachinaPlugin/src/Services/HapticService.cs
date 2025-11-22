namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class HapticService
    {
        private readonly Plugin _plugin;

        public HapticService(Plugin plugin)
        {
            this._plugin = plugin;
        }

        public void TriggerTimerStart()
        {
            this._plugin.PluginEvents.RaiseEvent("TimerStart");
            PluginLog.Verbose("Haptic: TimerStart");
        }

        public void TriggerTimerPause()
        {
            this._plugin.PluginEvents.RaiseEvent("TimerPause");
            PluginLog.Verbose("Haptic: TimerPause");
        }

        public void TriggerWorkComplete()
        {
            this._plugin.PluginEvents.RaiseEvent("WorkComplete");
            PluginLog.Verbose("Haptic: WorkComplete");
        }

        public void TriggerBreakComplete()
        {
            this._plugin.PluginEvents.RaiseEvent("BreakComplete");
            PluginLog.Verbose("Haptic: BreakComplete");
        }

        public void TriggerFocusModeOn()
        {
            this._plugin.PluginEvents.RaiseEvent("FocusModeOn");
            PluginLog.Verbose("Haptic: FocusModeOn");
        }

        public void TriggerFocusModeOff()
        {
            this._plugin.PluginEvents.RaiseEvent("FocusModeOff");
            PluginLog.Verbose("Haptic: FocusModeOff");
        }
    }
}
