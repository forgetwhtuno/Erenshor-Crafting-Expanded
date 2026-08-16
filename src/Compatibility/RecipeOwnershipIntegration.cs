using System;

namespace ErenshorCraftingExpanded
{
    // Optional provider contracts deliberately avoid referencing unpublished implementation types
    // from parallel workstreams. Providers may be registered at any time; absence must fail closed.
    public interface IRecipeCharacterIdentityProvider
    {
        bool TryGetStableCharacterIdentity(out string stableIdentity);
        string DescribeCapability();
    }

    public interface IRecipeBankTemplateProbe
    {
        bool TryCountTemplateByItemId(string templateItemId, out int quantity);
        string DescribeCapability();
    }

    public sealed class ForagingProgressionSnapshot
    {
        public int Level;
        public int Xp;
        public int XpToNextLevel;
        public string[] KnownResources = new string[0];
    }

    // This provider is intentionally stronger than a bank reader. A successful zero result must
    // mean every authoritative native storage/holding surface outside inventory+forge that can
    // retain this Template has been inspected (bank, AH/trade staging, cursor/buyback, etc. as
    // applicable to the current build). Only this capability may prove ordinary "Missing".
    public interface IRecipeTemplateAbsenceAuthority
    {
        bool TryCountAllOutsideInventoryAndForge(string templateItemId, out int quantity);
        string DescribeCapability();
    }

    public interface IForagingProgressionSource
    {
        bool TryGetProgression(out ForagingProgressionSnapshot snapshot);
        string DescribeCapability();
    }

    public static class RecipeOwnershipIntegration
    {
        private static IRecipeCharacterIdentityProvider _characterIdentity;
        private static IRecipeBankTemplateProbe _bankProbe;
        private static IRecipeTemplateAbsenceAuthority _absenceAuthority;
        private static IForagingProgressionSource _foragingProgression;

        public static void RegisterCharacterIdentityProvider(IRecipeCharacterIdentityProvider provider)
        {
            _characterIdentity = provider;
        }

        public static void UnregisterCharacterIdentityProvider(IRecipeCharacterIdentityProvider provider)
        {
            if (ReferenceEquals(_characterIdentity, provider)) _characterIdentity = null;
        }

        public static void RegisterBankTemplateProbe(IRecipeBankTemplateProbe provider)
        {
            _bankProbe = provider;
        }

        public static void UnregisterBankTemplateProbe(IRecipeBankTemplateProbe provider)
        {
            if (ReferenceEquals(_bankProbe, provider)) _bankProbe = null;
        }

        public static void RegisterTemplateAbsenceAuthority(IRecipeTemplateAbsenceAuthority provider)
        {
            _absenceAuthority = provider;
        }

        public static void UnregisterTemplateAbsenceAuthority(IRecipeTemplateAbsenceAuthority provider)
        {
            if (ReferenceEquals(_absenceAuthority, provider)) _absenceAuthority = null;
        }

        public static void RegisterForagingProgressionSource(IForagingProgressionSource provider)
        {
            _foragingProgression = provider;
        }

        public static void UnregisterForagingProgressionSource(IForagingProgressionSource provider)
        {
            if (ReferenceEquals(_foragingProgression, provider)) _foragingProgression = null;
        }

        internal static bool TryGetCharacterIdentity(out string stableIdentity)
        {
            stableIdentity = string.Empty;
            IRecipeCharacterIdentityProvider provider = _characterIdentity;
            if (provider == null) return false;
            try
            {
                string value;
                if (!provider.TryGetStableCharacterIdentity(out value) || string.IsNullOrEmpty(value)) return false;
                stableIdentity = value;
                return true;
            }
            catch { return false; }
        }

        internal static bool TryCountBankTemplate(string templateItemId, out int quantity)
        {
            quantity = 0;
            IRecipeBankTemplateProbe provider = _bankProbe;
            if (provider == null || string.IsNullOrEmpty(templateItemId)) return false;
            try
            {
                int value;
                if (!provider.TryCountTemplateByItemId(templateItemId, out value)) return false;
                quantity = value < 0 ? 0 : value;
                return true;
            }
            catch { return false; }
        }

        internal static bool TryCountAllExternalTemplateStorage(string templateItemId, out int quantity)
        {
            quantity = 0;
            IRecipeTemplateAbsenceAuthority provider = _absenceAuthority;
            if (provider == null || string.IsNullOrEmpty(templateItemId)) return false;
            try
            {
                int value;
                if (!provider.TryCountAllOutsideInventoryAndForge(templateItemId, out value)) return false;
                quantity = value < 0 ? 0 : value;
                return true;
            }
            catch { return false; }
        }

        internal static bool TryGetForagingProgression(out ForagingProgressionSnapshot snapshot)
        {
            snapshot = null;
            IForagingProgressionSource provider = _foragingProgression;
            if (provider == null) return false;
            try
            {
                ForagingProgressionSnapshot value;
                if (!provider.TryGetProgression(out value) || value == null) return false;
                snapshot = value;
                return true;
            }
            catch { return false; }
        }

        internal static string DescribeCharacterIdentityCapability()
        {
            return Describe(_characterIdentity, "character identity provider absent");
        }

        internal static string DescribeBankCapability()
        {
            return Describe(_bankProbe, "bank template inspection unavailable");
        }

        internal static string DescribeAbsenceAuthorityCapability()
        {
            return Describe(_absenceAuthority, "complete external Template absence probe unavailable");
        }

        internal static string DescribeForagingCapability()
        {
            return Describe(_foragingProgression, "foraging progression provider absent");
        }

        private static string Describe(object provider, string fallback)
        {
            if (provider == null) return fallback;
            try
            {
                IRecipeCharacterIdentityProvider character = provider as IRecipeCharacterIdentityProvider;
                if (character != null) return character.DescribeCapability() ?? fallback;
                IRecipeBankTemplateProbe bank = provider as IRecipeBankTemplateProbe;
                if (bank != null) return bank.DescribeCapability() ?? fallback;
                IRecipeTemplateAbsenceAuthority absence = provider as IRecipeTemplateAbsenceAuthority;
                if (absence != null) return absence.DescribeCapability() ?? fallback;
                IForagingProgressionSource forage = provider as IForagingProgressionSource;
                if (forage != null) return forage.DescribeCapability() ?? fallback;
            }
            catch { }
            return fallback;
        }

        internal static void ResetForPluginUnload()
        {
            // Do not unregister third-party providers on their behalf. Only drop references held by
            // this plugin assembly so a hot unload cannot retain another plugin instance.
            _characterIdentity = null;
            _bankProbe = null;
            _absenceAuthority = null;
            _foragingProgression = null;
        }

        internal static string RunSelfTests()
        {
            ResetForPluginUnload();
            string identity;
            if (TryGetCharacterIdentity(out identity)) return "FAIL absent character provider";
            int bank;
            if (TryCountBankTemplate("910100001", out bank)) return "FAIL absent bank provider";
            int external;
            if (TryCountAllExternalTemplateStorage("910100001", out external)) return "FAIL absent external storage authority";
            ForagingProgressionSnapshot forage;
            if (TryGetForagingProgression(out forage)) return "FAIL absent foraging provider";

            TestCharacterProvider cp = new TestCharacterProvider();
            RegisterCharacterIdentityProvider(cp);
            if (!TryGetCharacterIdentity(out identity) || identity != "stable-character") return "FAIL character provider";
            UnregisterCharacterIdentityProvider(cp);
            if (TryGetCharacterIdentity(out identity)) return "FAIL character provider unregister";

            TestBankProvider bp = new TestBankProvider();
            RegisterBankTemplateProbe(bp);
            if (!TryCountBankTemplate("910100001", out bank) || bank != 2) return "FAIL bank provider";
            UnregisterBankTemplateProbe(bp);

            TestAbsenceProvider ap = new TestAbsenceProvider();
            RegisterTemplateAbsenceAuthority(ap);
            if (!TryCountAllExternalTemplateStorage("910100001", out external) || external != 0) return "FAIL external storage authority";
            UnregisterTemplateAbsenceAuthority(ap);

            TestForagingProvider fp = new TestForagingProvider();
            RegisterForagingProgressionSource(fp);
            if (!TryGetForagingProgression(out forage) || forage.Level != 6 || forage.KnownResources.Length != 2) return "FAIL foraging provider";
            ResetForPluginUnload();
            return "PASS recipe ownership optional integration";
        }

        private sealed class TestCharacterProvider : IRecipeCharacterIdentityProvider
        {
            public bool TryGetStableCharacterIdentity(out string stableIdentity) { stableIdentity = "stable-character"; return true; }
            public string DescribeCapability() { return "test"; }
        }

        private sealed class TestBankProvider : IRecipeBankTemplateProbe
        {
            public bool TryCountTemplateByItemId(string templateItemId, out int quantity) { quantity = 2; return true; }
            public string DescribeCapability() { return "test"; }
        }

        private sealed class TestAbsenceProvider : IRecipeTemplateAbsenceAuthority
        {
            public bool TryCountAllOutsideInventoryAndForge(string templateItemId, out int quantity) { quantity = 0; return true; }
            public string DescribeCapability() { return "test"; }
        }

        private sealed class TestForagingProvider : IForagingProgressionSource
        {
            public bool TryGetProgression(out ForagingProgressionSnapshot snapshot)
            {
                snapshot = new ForagingProgressionSnapshot { Level = 6, Xp = 48, XpToNextLevel = 100, KnownResources = new string[] { "Wild Herb", "Cave Mushroom" } };
                return true;
            }
            public string DescribeCapability() { return "test"; }
        }
    }
}
