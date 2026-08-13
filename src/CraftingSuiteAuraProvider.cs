using System;
using System.Text;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorCraftingExpanded
{
    // Thin, optional Lunaris Aura transport adapter over the authoritative CraftingControlApi.
    // Erenshor-Three-Audit-Integration-Handoff/CONTRACT_RECONCILIATION.md: Hub speaks Aura only
    // and never reflects into private mod state; this class owns nothing beyond
    // formatting/parsing the bounded wire payloads and forwarding to CraftingControlApi. No
    // compile-time reference to ErenshorSuiteHub.dll, no gameplay/foraging logic duplicated here.
    // Developer scan/probe/asset-survey/debug controls are intentionally NOT exposed here.
    internal sealed class CraftingSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite." + CraftingControlApi.ModuleId + ".v1.";

        private IAuraProvider<string> _describe;
        private IAuraProvider<string> _basicSettings;
        private IAuraProvider<string, string, string> _settingSet;
        private IAuraProvider<string, string, string> _action;
        private string _version = "0.0.0";
        private ILog _log;

        internal bool Registered { get; private set; }

        internal void Register(LunarisPlugin owner)
        {
            if (owner == null) return;
            _log = owner.Logging;
            try
            {
                LunarisPluginAttribute attr = Attribute.GetCustomAttribute(owner.GetType(), typeof(LunarisPluginAttribute)) as LunarisPluginAttribute;
                if (attr != null && !string.IsNullOrEmpty(attr.Version)) _version = attr.Version;

                _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
                _describe.RegisterFunc(Describe);

                _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
                _basicSettings.RegisterFunc(BasicSettings);

                _settingSet = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
                _settingSet.RegisterFunc(SetSetting);

                _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
                _action.RegisterFunc(InvokeAction);

                Registered = true;
            }
            catch (Exception ex)
            {
                Registered = false;
                if (_log != null) { try { _log.LogError("[Erenshor Crafting Expanded] Suite Aura provider registration failed: " + ex.GetType().Name); } catch { } }
                Unregister();
            }
        }

        // Provider lifecycle contract: explicitly unregister every Aura handler on OnDestroy so
        // Hub sees this module disappear immediately rather than calling into a torn-down plugin.
        internal void Unregister()
        {
            SafeUnregister(_describe); _describe = null;
            SafeUnregister(_basicSettings); _basicSettings = null;
            SafeUnregister(_settingSet); _settingSet = null;
            SafeUnregister(_action); _action = null;
            Registered = false;
        }

        private static void SafeUnregister(IAuraProvider provider)
        {
            if (provider == null) return;
            try { provider.UnregisterFunc(); } catch { }
        }

        private string Describe()
        {
            try
            {
                CraftingControlState s = CraftingControlApi.GetBasicState();
                StringBuilder sb = new StringBuilder(256);
                AppendField(sb, "protocol", "1");
                AppendField(sb, "module", CraftingControlApi.ModuleId);
                AppendField(sb, "display", "Erenshor Crafting Expanded");
                AppendField(sb, "version", _version);
                AppendField(sb, "summary", s.Enabled ? "Crafting Expanded enabled" : "Crafting Expanded disabled");
                AppendField(sb, "status", CraftingControlApi.GetStatus());
                AppendField(sb, "actions", "openPanel,closePanel,resetPanel,resetLauncher");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "protocol=1&module=" + CraftingControlApi.ModuleId + "&display=Erenshor+Crafting+Expanded&version=" +
                    Uri.EscapeDataString(_version) + "&warning=" + Uri.EscapeDataString(ex.GetType().Name);
            }
        }

        private string BasicSettings()
        {
            try
            {
                CraftingControlState s = CraftingControlApi.GetBasicState();
                StringBuilder sb = new StringBuilder(256);
                AppendBoolSettingLine(sb, "showLauncher", "Show Crafting launcher", CraftingControlApi.GetShowLauncher());
                AppendBoolSettingLine(sb, "enabled", "Crafting Expanded", s.Enabled);
                AppendBoolSettingLine(sb, "foraging", "Foraging", s.ForagingEnabled);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        // Every mutating call is revalidated by CraftingControlApi/CraftingController; Hub is not
        // authorization. "ok" means accepted, not necessarily synchronously reflected in the
        // panel - Hub re-reads state on a later frame via describe/settings.basic.
        private string SetSetting(string settingId, string value)
        {
            try
            {
                bool boolValue;
                if (!SuiteUiControlPolicy.TryParseBool(value, out boolValue)) return "invalid value";
                if (string.Equals(settingId, "showLauncher", StringComparison.Ordinal))
                    return CraftingControlApi.SetShowLauncher(boolValue) ? "ok" : "rejected";
                if (string.Equals(settingId, "enabled", StringComparison.Ordinal))
                    return CraftingControlApi.SetEnabled(boolValue) ? "ok" : "rejected";
                if (string.Equals(settingId, "foraging", StringComparison.Ordinal))
                    return CraftingControlApi.SetForagingEnabled(boolValue) ? "ok" : "rejected";
                return "unknown setting";
            }
            catch (Exception ex) { return "error:" + ex.GetType().Name; }
        }

        private string InvokeAction(string actionId, string argument)
        {
            try
            {
                switch (SuiteUiControlPolicy.ParsePanelAction(actionId))
                {
                    case SuitePanelAction.OpenPanel: return CraftingControlApi.OpenPanel() ? "ok" : "rejected";
                    case SuitePanelAction.ClosePanel: return CraftingControlApi.ClosePanel() ? "ok" : "rejected";
                    case SuitePanelAction.ResetPanel: CraftingControlApi.ResetPanelPosition(); return "ok";
                    case SuitePanelAction.ResetLauncher: CraftingControlApi.ResetLauncherPosition(); return "ok";
                    default: return "unknown action";
                }
            }
            catch (Exception ex) { return "error:" + ex.GetType().Name; }
        }

        private static void AppendField(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(key).Append('=').Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        private static void AppendBoolSettingLine(StringBuilder sb, string id, string label, bool value)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("id=").Append(Uri.EscapeDataString(id));
            sb.Append("&label=").Append(Uri.EscapeDataString(label));
            sb.Append("&tier=basic&type=bool&value=").Append(value ? "true" : "false");
            sb.Append("&mutable=true");
        }
    }
}
