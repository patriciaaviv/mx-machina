namespace Loupedeck.MXMachinaPlugin
{
    using System;

    // This class contains the plugin-level logic of the Loupedeck plugin.

    public class MXMachinaPlugin : Plugin
    {
        // Gets a value indicating whether this is an API-only plugin.
        public override Boolean UsesApplicationApiOnly => true;

        // Gets a value indicating whether this is a Universal plugin or an Application plugin.
        public override Boolean HasNoApplication => true;

        // Haptic service instance
        public HapticService Haptics { get; private set; }

        // Initializes a new instance of the plugin class.
        public MXMachinaPlugin()
        {
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);
        }

        // This method is called when the plugin is loaded.
        public override void Load()
        {
            // Register haptic events
            this.PluginEvents.AddEvent("TimerStart", "Timer Started", "Haptic feedback when timer starts");
            this.PluginEvents.AddEvent("TimerPause", "Timer Paused", "Haptic feedback when timer is paused");
            this.PluginEvents.AddEvent("WorkComplete", "Work Session Complete", "Haptic feedback when work session ends");
            this.PluginEvents.AddEvent("BreakComplete", "Break Complete", "Haptic feedback when break ends");
            this.PluginEvents.AddEvent("FocusModeOn", "Focus Mode Enabled", "Haptic feedback when focus mode is enabled");
            this.PluginEvents.AddEvent("FocusModeOff", "Focus Mode Disabled", "Haptic feedback when focus mode is disabled");

            // Initialize haptic service
            this.Haptics = new HapticService(this);

            // Initialize haptic triggers for timer events
            PomodoroService.InitializeHaptics(this.Haptics);

            PluginLog.Info("Haptic events registered and service initialized");
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
        }
    }
}