using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cm6206DualRouter;

internal static class NeonSchemaBuilder
{
    public static JsonObject BuildSchema()
    {
        return new JsonObject
        {
            ["version"] = 1,
            ["screens"] = new JsonObject
            {
                ["telemetry_config"] = BuildTelemetryConfigScreen(),
                ["advanced_settings"] = BuildAdvancedSettingsScreen(),
                ["instrument_config"] = BuildInstrumentConfigScreen(),
                ["spatial_config"] = BuildSpatialConfigScreen(),
                ["soundscape_config"] = BuildSoundscapeConfigScreen(),
                ["soundscape_groups"] = BuildSoundscapeGroupsScreen(),
                ["soundscape_group_edit"] = BuildSoundscapeGroupEditScreen(),
                ["soundscape_overrides"] = BuildSoundscapeOverridesScreen(),
                ["soundscape_override_edit"] = BuildSoundscapeOverrideEditScreen()
            }
        };
    }

    private static JsonObject BuildInstrumentConfigScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        JsonObject Button(string id, string textKey, string action) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.config.instruments",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.config.instruments"),
                    Button("openEditor", "bassshakertelemetry.config.instruments_open_editor", "openInstrumentsEditor")
                )
            }
        };
    }

    private static JsonObject BuildSoundscapeGroupsScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        JsonObject Button(string id, string textKey, string action) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.soundscape.groups_title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.soundscape.groups_title"),
                    Button("add", "bassshakertelemetry.soundscape.group_add", "addSoundscapeGroup")
                )
            }
        };
    }

    private static JsonObject BuildSoundscapeGroupEditScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.soundscape.group_edit_title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.soundscape.group_edit_title")
                )
            }
        };
    }

    private static JsonObject BuildSoundscapeOverridesScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        JsonObject Button(string id, string textKey, string action) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.soundscape.overrides_title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.soundscape.overrides_desc"),
                    Button("add", "bassshakertelemetry.soundscape.override_add", "addSoundscapeOverride")
                )
            }
        };
    }

    private static JsonObject BuildSoundscapeOverrideEditScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.soundscape.override_edit_title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.soundscape.override_edit_title")
                )
            }
        };
    }

    private static JsonObject BuildSoundscapeConfigScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        JsonObject Toggle(string id, string textKey, string bind) => new JsonObject
        {
            ["type"] = "toggle",
            ["id"] = id,
            ["textKey"] = textKey,
            ["bind"] = bind
        };

        JsonObject Button(string id, string textKey, string action) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.soundscape.title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.soundscape.section.mode"),
                    Toggle("soundscapeEnabled", "bassshakertelemetry.spatial.soundscape_enabled", "soundScapeEnabled"),
                    Button("groups", "bassshakertelemetry.soundscape.groups", "openSoundscapeGroups"),
                    Button("overrides", "bassshakertelemetry.soundscape.overrides", "openSoundscapeOverrides"),

                    Label("bassshakertelemetry.soundscape.section.routing"),
                    Button("routeRoad", "bassshakertelemetry.soundscape.route.road", "cycleSoundscapeRoute:road"),
                    Button("routeDamage", "bassshakertelemetry.soundscape.route.damage", "cycleSoundscapeRoute:damage"),
                    Button("routeBiome", "bassshakertelemetry.soundscape.route.biome", "cycleSoundscapeRoute:biome_chime"),
                    Button("routeAccel", "bassshakertelemetry.soundscape.route.accel", "cycleSoundscapeRoute:accel_bump"),
                    Button("routeSound", "bassshakertelemetry.soundscape.route.sound", "cycleSoundscapeRoute:sound"),
                    Button("routeGameplay", "bassshakertelemetry.soundscape.route.gameplay", "cycleSoundscapeRoute:gameplay"),
                    Button("routeFootsteps", "bassshakertelemetry.soundscape.route.footsteps", "cycleSoundscapeRoute:footsteps"),
                    Button("routeMounted", "bassshakertelemetry.soundscape.route.mounted", "cycleSoundscapeRoute:mounted"),
                    Button("routeMining", "bassshakertelemetry.soundscape.route.mining", "cycleSoundscapeRoute:mining_swing"),
                    Button("routeCustom", "bassshakertelemetry.soundscape.route.custom", "cycleSoundscapeRoute:custom")
                )
            }
        };
    }

    private static JsonObject BuildSpatialConfigScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject Toggle(string id, string textKey, string bind) => new JsonObject
        {
            ["type"] = "toggle",
            ["id"] = id,
            ["textKey"] = textKey,
            ["bind"] = bind
        };

        JsonObject Slider(string id, string textKey, string bind, double min, double max, double step, string format) => new JsonObject
        {
            ["type"] = "slider",
            ["id"] = id,
            ["textKey"] = textKey,
            ["bind"] = bind,
            ["min"] = min,
            ["max"] = max,
            ["step"] = step,
            ["format"] = format
        };

        JsonObject Button(string id, string textKey, string action) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.spatial.title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Toggle("soundscapeEnabled", "bassshakertelemetry.spatial.soundscape_enabled", "soundScapeEnabled"),
                    Toggle("panningEnabled", "bassshakertelemetry.spatial.panning_enabled", "soundScapeSpatialEnabled"),
                    Slider("distanceAtten", "bassshakertelemetry.spatial.distance_atten", "soundScapeSpatialDistanceAttenStrength", 0.0, 1.0, 0.01, "percent"),
                    Button("openRouting", "bassshakertelemetry.spatial.open_routing", "openSoundscape"),
                    Button("openBusRouting", "bassshakertelemetry.spatial.open_bus_routing", "openSpatialBusRouting"),
                    Button("openCalibration", "bassshakertelemetry.spatial.open_calibration", "openSpatialCalibration"),
                    Button("openDebugger", "bassshakertelemetry.spatial.open_debugger", "openSpatialDebugger")
                )
            }
        };
    }

    private static JsonObject BuildTelemetryConfigScreen()
    {
        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.config.title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "button",
                        ["id"] = "outputDevice",
                        ["textKey"] = "bassshakertelemetry.config.output_device",
                        ["action"] = "openOutputDevice",
                        ["bind"] = "outputDeviceName"
                    },
                    new JsonObject
                    {
                        ["type"] = "slider",
                        ["id"] = "masterVolume",
                        ["textKey"] = "bassshakertelemetry.config.master_volume",
                        ["bind"] = "masterVolume",
                        ["min"] = 0.0,
                        ["max"] = 1.0,
                        ["step"] = 0.01,
                        ["format"] = "percent"
                    },
                    new JsonObject
                    {
                        ["type"] = "button",
                        ["id"] = "advanced",
                        ["textKey"] = "bassshakertelemetry.config.advanced",
                        ["action"] = "openAdvanced"
                    },
                    new JsonObject
                    {
                        ["type"] = "button",
                        ["id"] = "soundscape",
                        ["textKey"] = "bassshakertelemetry.soundscape.open",
                        ["action"] = "openSoundscape"
                    },
                    new JsonObject
                    {
                        ["type"] = "panel",
                        ["layout"] = "horizontal",
                        ["spacing"] = 10,
                        ["children"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "toggle",
                                ["id"] = "damage",
                                ["textKey"] = "bassshakertelemetry.config.damage_enabled",
                                ["bind"] = "damageBurstEnabled"
                            },
                            new JsonObject
                            {
                                ["type"] = "toggle",
                                ["id"] = "biome",
                                ["textKey"] = "bassshakertelemetry.config.biome_enabled",
                                ["bind"] = "biomeChimeEnabled"
                            }
                        }
                    },
                    new JsonObject
                    {
                        ["type"] = "panel",
                        ["layout"] = "horizontal",
                        ["spacing"] = 10,
                        ["children"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "toggle",
                                ["id"] = "road",
                                ["textKey"] = "bassshakertelemetry.config.road_enabled",
                                ["bind"] = "roadTextureEnabled"
                            },
                            new JsonObject
                            {
                                ["type"] = "toggle",
                                ["id"] = "sound",
                                ["textKey"] = "bassshakertelemetry.config.sound_enabled",
                                ["bind"] = "soundHapticsEnabled"
                            }
                        }
                    },
                    new JsonObject
                    {
                        ["type"] = "toggle",
                        ["id"] = "gameplay",
                        ["textKey"] = "bassshakertelemetry.config.gameplay_enabled",
                        ["bind"] = "gameplayHapticsEnabled"
                    },
                    new JsonObject
                    {
                        ["type"] = "toggle",
                        ["id"] = "accessibilityHud",
                        ["textKey"] = "bassshakertelemetry.config.accessibility_hud",
                        ["bind"] = "accessibilityHudEnabled"
                    }
                }
            }
        };
    }

    private static JsonObject BuildAdvancedSettingsScreen()
    {
        JsonArray Children(params JsonNode[] nodes) => new JsonArray(nodes);

        JsonObject HRow(int spacing, params JsonNode[] nodes) => new JsonObject
        {
            ["type"] = "panel",
            ["layout"] = "horizontal",
            ["spacing"] = spacing,
            ["children"] = Children(nodes)
        };

        JsonObject Label(string textKey) => new JsonObject
        {
            ["type"] = "label",
            ["textKey"] = textKey
        };

        JsonObject Slider(string id, string textKey, string bind, double min, double max, double step, string format) => new JsonObject
        {
            ["type"] = "slider",
            ["id"] = id,
            ["textKey"] = textKey,
            ["bind"] = bind,
            ["min"] = min,
            ["max"] = max,
            ["step"] = step,
            ["format"] = format
        };

        JsonObject Button(string id, string textKey, string action) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action
        };

        JsonObject SmallButton(string id, string textKey, string action, int width) => new JsonObject
        {
            ["type"] = "button",
            ["id"] = id,
            ["textKey"] = textKey,
            ["action"] = action,
            ["width"] = width
        };

        JsonObject Toggle(string id, string textKey, string bind) => new JsonObject
        {
            ["type"] = "toggle",
            ["id"] = id,
            ["textKey"] = textKey,
            ["bind"] = bind
        };

        return new JsonObject
        {
            ["titleKey"] = "bassshakertelemetry.config.advanced_title",
            ["root"] = new JsonObject
            {
                ["type"] = "panel",
                ["layout"] = "vertical",
                ["padding"] = 0,
                ["spacing"] = 6,
                ["children"] = Children(
                    Label("bassshakertelemetry.config.effect_volumes"),

                    HRow(6,
                        Slider("roadGain", "bassshakertelemetry.config.road_gain", "roadTextureGain", 0.0, 0.5, 0.01, "percentRange"),
                        SmallButton("roadTest", "bassshakertelemetry.config.test", "testRoadTexture", 64)
                    ),

                    HRow(6,
                        Slider("damageGain", "bassshakertelemetry.config.damage_gain", "damageBurstGain", 0.0, 1.0, 0.01, "percentRange"),
                        SmallButton("damageTest", "bassshakertelemetry.config.test", "testDamageBurst", 64)
                    ),

                    HRow(6,
                        Slider("biomeGain", "bassshakertelemetry.config.biome_gain", "biomeChimeGain", 0.0, 1.0, 0.01, "percentRange"),
                        SmallButton("biomeTest", "bassshakertelemetry.config.test", "testBiomeChime", 64)
                    ),

                    HRow(6,
                        Slider("accelGain", "bassshakertelemetry.config.accel_gain", "accelBumpGain", 0.0, 1.0, 0.01, "percentRange"),
                        SmallButton("accelTest", "bassshakertelemetry.config.test", "testAccelBump", 64)
                    ),

                    HRow(6,
                        Slider("soundGain", "bassshakertelemetry.config.sound_gain", "soundHapticsGain", 0.0, 2.0, 0.01, "percentRange"),
                        SmallButton("soundTest", "bassshakertelemetry.config.test", "testSoundHaptics", 64)
                    ),

                    HRow(6,
                        Slider("gameplayGain", "bassshakertelemetry.config.gameplay_gain", "gameplayHapticsGain", 0.0, 2.0, 0.01, "percentRange"),
                        SmallButton("gameplayTest", "bassshakertelemetry.config.test", "testGameplayHaptics", 64)
                    ),

                    HRow(6,
                        Slider("footstepGain", "bassshakertelemetry.config.footstep_gain", "footstepHapticsGain", 0.0, 1.0, 0.01, "percentRange"),
                        SmallButton("footstepTest", "bassshakertelemetry.config.test", "testFootsteps", 64)
                    ),

                    HRow(6,
                        Slider("mountedGain", "bassshakertelemetry.config.mounted_gain", "mountedHapticsGain", 0.0, 1.0, 0.01, "percentRange"),
                        SmallButton("mountedTest", "bassshakertelemetry.config.test", "testMountedHooves", 64)
                    ),

                    HRow(6,
                        Slider("miningSwingGain", "bassshakertelemetry.config.mining_swing_gain", "miningSwingHapticsGain", 0.0, 1.0, 0.01, "percentRange"),
                        SmallButton("miningSwingTest", "bassshakertelemetry.config.test", "testMiningSwing", 64)
                    ),

                    Label("bassshakertelemetry.config.timing"),
                    Slider("damageMs", "bassshakertelemetry.config.damage_ms", "damageBurstMs", 20, 250, 1, "ms"),
                    Slider("accelMs", "bassshakertelemetry.config.accel_ms", "accelBumpMs", 20, 200, 1, "ms"),
                    Slider("soundCooldownMs", "bassshakertelemetry.config.sound_cooldown_ms", "soundHapticsCooldownMs", 0, 250, 1, "ms"),
                    Slider("gameplayCooldownMs", "bassshakertelemetry.config.gameplay_cooldown_ms", "gameplayHapticsCooldownMs", 0, 400, 1, "ms"),
                    Slider("miningPeriodMs", "bassshakertelemetry.config.mining_period_ms", "gameplayMiningPulsePeriodMs", 60, 350, 1, "ms"),

                    Label("bassshakertelemetry.config.audio"),
                    Button("buffer", "bassshakertelemetry.config.output_buffer", "cycleBufferChoice"),
                    Button("latency", "bassshakertelemetry.config.latency_test_off", "toggleLatencyTest"),
                    Toggle("debugOverlay", "bassshakertelemetry.config.debug_overlay", "debugOverlayEnabled"),
                    Button("demo", "bassshakertelemetry.config.demo_run", "toggleDemo"),

                    Label("bassshakertelemetry.config.tone_shaping"),
                    Toggle("outputEq", "bassshakertelemetry.config.output_eq", "outputEqEnabled"),
                    Slider("outputEqFreq", "bassshakertelemetry.config.output_eq_freq", "outputEqFreqHz", 10, 120, 1, "hz"),
                    Slider("outputEqGain", "bassshakertelemetry.config.output_eq_gain", "outputEqGainDb", -12, 12, 1, "db"),
                    Toggle("smartVolume", "bassshakertelemetry.config.smart_volume", "smartVolumeEnabled"),
                    Slider("smartVolumeTarget", "bassshakertelemetry.config.smart_volume_target", "smartVolumeTargetPct", 10, 90, 1, "pct"),

                    Label("bassshakertelemetry.config.calibration"),
                    Button("cal30", "bassshakertelemetry.config.cal_tone_30hz", "testCalibrationTone30Hz"),
                    Button("cal60", "bassshakertelemetry.config.cal_tone_60hz", "testCalibrationTone60Hz"),
                    Button("calSweep", "bassshakertelemetry.config.cal_sweep_20_120hz", "testCalibrationSweep"),
                    Button("calStop", "bassshakertelemetry.config.cal_stop", "stopCalibration"),

                    Label("bassshakertelemetry.config.spatial"),
                    Button("spatial", "bassshakertelemetry.config.spatial_open", "openSpatial"),

                    Label("bassshakertelemetry.config.instruments"),
                    Button("instruments", "bassshakertelemetry.config.instruments", "openInstruments")
                )
            }
        };
    }

    public static string ToJson(JsonNode node)
    {
        return node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
