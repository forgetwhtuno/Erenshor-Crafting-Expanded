using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ErenshorCraftingExpanded
{
    public enum ProductionRecipeBindingLoadState
    {
        Missing = 0,
        Loaded = 1,
        Malformed = 2,
        UnsupportedVersion = 3
    }

    public sealed class ProductionRecipeBinding
    {
        public string RecipeKey = string.Empty;
        public string TemplateItemId = string.Empty;
        public ProductionRecipeContentKind ContentKind;
        public string DonorTemplateId = string.Empty;
        public string OutputItemId = string.Empty;
        public int OutputItemValue;
        public string OutputEffectType = string.Empty;
        public string DonorIngredientFingerprint = string.Empty;
    }

    public sealed class ProductionRecipeBindingDocument
    {
        public readonly List<ProductionRecipeBinding> Bindings = new List<ProductionRecipeBinding>();
        public ProductionRecipeBindingLoadState LoadState = ProductionRecipeBindingLoadState.Missing;

        public ProductionRecipeBinding Get(string recipeKey)
        {
            for (int i = 0; i < Bindings.Count; i++)
                if (string.Equals(Bindings[i].RecipeKey, recipeKey, StringComparison.Ordinal)) return Bindings[i];
            return null;
        }
    }

    public static class ProductionRecipeBindingCodec
    {
        private const string Header = "ERENSHOR_CRAFTING_NATIVE_RECIPE_BINDINGS_V1";

        public static string Encode(ProductionRecipeBindingDocument document)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.AppendLine(Header);
            if (document == null) return sb.ToString();
            List<ProductionRecipeBinding> sorted = new List<ProductionRecipeBinding>(document.Bindings);
            sorted.Sort(delegate(ProductionRecipeBinding a, ProductionRecipeBinding b) { return string.Compare(a.RecipeKey, b.RecipeKey, StringComparison.Ordinal); });
            for (int i = 0; i < sorted.Count; i++)
            {
                ProductionRecipeBinding b = sorted[i];
                if (!IsValid(b)) continue;
                sb.Append(b.RecipeKey).Append('\t').Append(b.TemplateItemId).Append('\t').Append((int)b.ContentKind).Append('\t')
                    .Append(b.DonorTemplateId).Append('\t').Append(b.OutputItemId).Append('\t').Append(b.OutputItemValue).Append('\t')
                    .Append(string.IsNullOrEmpty(b.OutputEffectType) ? "-" : b.OutputEffectType).Append('\t').Append(b.DonorIngredientFingerprint).AppendLine();
            }
            return sb.ToString();
        }

        public static bool TryDecode(string text, out ProductionRecipeBindingDocument document)
        {
            document = new ProductionRecipeBindingDocument();
            if (string.IsNullOrEmpty(text)) { document.LoadState = ProductionRecipeBindingLoadState.Malformed; return false; }
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length == 0 || !string.Equals(lines[0].Trim(), Header, StringComparison.Ordinal))
            {
                document.LoadState = lines.Length > 0 && lines[0].StartsWith("ERENSHOR_CRAFTING_NATIVE_RECIPE_BINDINGS_", StringComparison.Ordinal)
                    ? ProductionRecipeBindingLoadState.UnsupportedVersion : ProductionRecipeBindingLoadState.Malformed;
                return false;
            }
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> templateIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                string[] fields = line.Split('\t');
                int kindValue;
                int outputValue;
                if (fields.Length != 8 || !int.TryParse(fields[2], out kindValue) || kindValue < 0 || kindValue > 1 || !int.TryParse(fields[5], out outputValue) || outputValue <= 0)
                { document.Bindings.Clear(); document.LoadState = ProductionRecipeBindingLoadState.Malformed; return false; }
                ProductionRecipeBinding b = new ProductionRecipeBinding();
                b.RecipeKey = fields[0]; b.TemplateItemId = fields[1]; b.ContentKind = (ProductionRecipeContentKind)kindValue;
                b.DonorTemplateId = fields[3]; b.OutputItemId = fields[4]; b.OutputItemValue = outputValue;
                b.OutputEffectType = string.Equals(fields[6], "-", StringComparison.Ordinal) ? string.Empty : fields[6];
                b.DonorIngredientFingerprint = fields[7];
                if (!IsValid(b) || !keys.Add(b.RecipeKey) || !templateIds.Add(b.TemplateItemId))
                { document.Bindings.Clear(); document.LoadState = ProductionRecipeBindingLoadState.Malformed; return false; }
                document.Bindings.Add(b);
            }
            document.LoadState = ProductionRecipeBindingLoadState.Loaded;
            return true;
        }

        public static bool IsValid(ProductionRecipeBinding binding)
        {
            if (binding == null || string.IsNullOrEmpty(binding.RecipeKey) || string.IsNullOrEmpty(binding.TemplateItemId) ||
                string.IsNullOrEmpty(binding.DonorTemplateId) || string.IsNullOrEmpty(binding.OutputItemId) || binding.OutputItemValue <= 0 || string.IsNullOrEmpty(binding.DonorIngredientFingerprint)) return false;
            ProductionRecipePlanEntry plan = ProductionRecipePlan.Get(binding.RecipeKey);
            if (plan == null || !string.Equals(plan.TemplateItemId, binding.TemplateItemId, StringComparison.Ordinal) || plan.ContentKind != binding.ContentKind) return false;
            if (binding.ContentKind == ProductionRecipeContentKind.ActivatedUtility && string.IsNullOrEmpty(binding.OutputEffectType)) return false;
            if (binding.ContentKind == ProductionRecipeContentKind.Foundation && !string.IsNullOrEmpty(binding.OutputEffectType)) return false;
            return true;
        }

        internal static string RunSelfTests()
        {
            ProductionRecipeBindingDocument source = new ProductionRecipeBindingDocument();
            source.LoadState = ProductionRecipeBindingLoadState.Loaded;
            ProductionRecipeBinding b = new ProductionRecipeBinding();
            b.RecipeKey = "crafting.herbal_preparation"; b.TemplateItemId = "910100011"; b.ContentKind = ProductionRecipeContentKind.ActivatedUtility;
            b.DonorTemplateId = "100"; b.OutputItemId = "200"; b.OutputItemValue = 7; b.OutputEffectType = "NativeEffect"; b.DonorIngredientFingerprint = "10=2,11=1";
            source.Bindings.Add(b);
            ProductionRecipeBindingDocument decoded;
            if (!TryDecode(Encode(source), out decoded) || decoded.Bindings.Count != 1) return "FAIL binding round trip";
            if (!string.Equals(decoded.Bindings[0].DonorIngredientFingerprint, "10=2,11=1", StringComparison.Ordinal)) return "FAIL binding fingerprint round trip";
            if (decoded.Bindings[0].OutputItemValue != 7 || !string.Equals(decoded.Bindings[0].OutputEffectType, "NativeEffect", StringComparison.Ordinal)) return "FAIL binding output fingerprint round trip";
            ProductionRecipeBinding invalidUtility = new ProductionRecipeBinding();
            invalidUtility.RecipeKey = "crafting.herbal_preparation"; invalidUtility.TemplateItemId = "910100011"; invalidUtility.ContentKind = ProductionRecipeContentKind.ActivatedUtility;
            invalidUtility.DonorTemplateId = "100"; invalidUtility.OutputItemId = "200"; invalidUtility.OutputItemValue = 7; invalidUtility.DonorIngredientFingerprint = "10=1";
            if (IsValid(invalidUtility)) return "FAIL activated binding without effect accepted";
            ProductionRecipeBinding invalidFoundation = new ProductionRecipeBinding();
            invalidFoundation.RecipeKey = "crafting.basic_supply"; invalidFoundation.TemplateItemId = "910100010"; invalidFoundation.ContentKind = ProductionRecipeContentKind.Foundation;
            invalidFoundation.DonorTemplateId = "101"; invalidFoundation.OutputItemId = "201"; invalidFoundation.OutputItemValue = 5; invalidFoundation.OutputEffectType = "UnexpectedEffect"; invalidFoundation.DonorIngredientFingerprint = "11=1";
            if (IsValid(invalidFoundation)) return "FAIL foundation binding with effect accepted";
            if (TryDecode("ERENSHOR_CRAFTING_NATIVE_RECIPE_BINDINGS_V2\n", out decoded) || decoded.LoadState != ProductionRecipeBindingLoadState.UnsupportedVersion) return "FAIL future binding version";
            if (TryDecode(Header + "\n" + "crafting.herbal_preparation\t910100011\t1\t100\t200\t7\tNativeEffect\t10=1\n" + "crafting.herbal_preparation\t910100011\t1\t101\t201\t8\tNativeEffect\t11=1\n", out decoded)) return "FAIL duplicate binding accepted";
            return "PASS production recipe binding codec";
        }
    }

    internal static class ProductionRecipeBindingStore
    {
        internal static string LastError = string.Empty;
        internal static string LastRecovery = string.Empty;

        internal static ProductionRecipeBindingDocument Load(string path)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            ProductionRecipeBindingDocument document = new ProductionRecipeBindingDocument();
            if (string.IsNullOrEmpty(path)) { document.LoadState = ProductionRecipeBindingLoadState.Missing; return document; }
            try
            {
                // A future-version primary is authoritative. Never silently fall back to an older
                // binding and remap stable recipe ids under a downgraded build.
                string primaryText;
                string primaryError;
                if (File.Exists(path) && AtomicTextSidecar.TryReadPrimary(path, out primaryText, out primaryError))
                {
                    ProductionRecipeBindingDocument primary;
                    if (!ProductionRecipeBindingCodec.TryDecode(primaryText, out primary) && primary.LoadState == ProductionRecipeBindingLoadState.UnsupportedVersion)
                    {
                        LastError = "native recipe binding file is from a newer version; production recipe remapping is disabled";
                        return primary;
                    }
                }

                string text;
                string recoveryError;
                SidecarRecoverySource source;
                if (!AtomicTextSidecar.TryLoadNewestValid(path, IsRecoverableText, out text, out source, out recoveryError))
                {
                    if (!AtomicTextSidecar.HasAnyCandidate(path)) { document.LoadState = ProductionRecipeBindingLoadState.Missing; return document; }
                    document.LoadState = ProductionRecipeBindingLoadState.Malformed;
                    LastError = string.IsNullOrEmpty(recoveryError)
                        ? "native recipe binding file has no valid recovery candidate; production recipe remapping is disabled"
                        : "native recipe binding recovery failed: " + recoveryError;
                    return document;
                }

                if (!ProductionRecipeBindingCodec.TryDecode(text, out document))
                {
                    LastError = document.LoadState == ProductionRecipeBindingLoadState.UnsupportedVersion
                        ? "native recipe binding file is from a newer version; production recipe remapping is disabled"
                        : "native recipe binding file is malformed; production recipe remapping is disabled";
                    return document;
                }
                if (source != SidecarRecoverySource.Primary) LastRecovery = source.ToString().ToLowerInvariant();
                return document;
            }
            catch (Exception ex)
            {
                document.LoadState = ProductionRecipeBindingLoadState.Malformed;
                LastError = "native recipe binding load failed: " + ex.GetType().Name;
                return document;
            }
        }

        internal static bool Save(string path, ProductionRecipeBindingDocument document)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            if (string.IsNullOrEmpty(path) || document == null) { LastError = "native recipe binding save path unavailable"; return false; }
            if (document.LoadState == ProductionRecipeBindingLoadState.UnsupportedVersion)
            {
                LastError = "native recipe binding file is from a newer version; refusing to overwrite it";
                return false;
            }
            try
            {
                string error;
                if (!AtomicTextSidecar.WriteAtomic(path, ProductionRecipeBindingCodec.Encode(document), IsRecoverableText, out error))
                {
                    LastError = "native recipe binding save failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error);
                    return false;
                }
                document.LoadState = ProductionRecipeBindingLoadState.Loaded;
                return true;
            }
            catch (Exception ex) { LastError = "native recipe binding save failed: " + ex.GetType().Name; return false; }
        }

        private static bool IsRecoverableText(string text)
        {
            ProductionRecipeBindingDocument document;
            return ProductionRecipeBindingCodec.TryDecode(text, out document) && document.LoadState == ProductionRecipeBindingLoadState.Loaded;
        }

        internal static string RunStoreSelfTests()
        {
            string root = Path.Combine(Path.GetTempPath(), "ece-production-binding-tests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "bindings.txt");
            try
            {
                ProductionRecipeBindingDocument document = new ProductionRecipeBindingDocument();
                document.LoadState = ProductionRecipeBindingLoadState.Loaded;
                ProductionRecipeBinding b = new ProductionRecipeBinding();
                b.RecipeKey = "crafting.herbal_preparation"; b.TemplateItemId = "910100011"; b.ContentKind = ProductionRecipeContentKind.ActivatedUtility;
                b.DonorTemplateId = "100"; b.OutputItemId = "200"; b.OutputItemValue = 7; b.OutputEffectType = "NativeEffect"; b.DonorIngredientFingerprint = "10=1";
                document.Bindings.Add(b);
                if (!Save(path, document)) return "FAIL production binding store save " + LastError;
                ProductionRecipeBindingDocument loaded = Load(path);
                if (loaded.LoadState != ProductionRecipeBindingLoadState.Loaded || loaded.Bindings.Count != 1) return "FAIL production binding store load";

                ProductionRecipeBindingDocument newer = new ProductionRecipeBindingDocument();
                newer.LoadState = ProductionRecipeBindingLoadState.Loaded;
                ProductionRecipeBinding b2 = new ProductionRecipeBinding();
                b2.RecipeKey = "crafting.basic_supply"; b2.TemplateItemId = "910100010"; b2.ContentKind = ProductionRecipeContentKind.Foundation;
                b2.DonorTemplateId = "101"; b2.OutputItemId = "201"; b2.OutputItemValue = 5; b2.OutputEffectType = string.Empty; b2.DonorIngredientFingerprint = "11=1";
                newer.Bindings.Add(b2);
                File.WriteAllText(path + ".tmp", ProductionRecipeBindingCodec.Encode(newer), Encoding.UTF8);
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(path + ".tmp", new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
                ProductionRecipeBindingDocument recovered = Load(path);
                if (recovered.Get("crafting.basic_supply") == null || LastRecovery != "temp") return "FAIL production binding temp recovery";

                File.WriteAllText(path, "ERENSHOR_CRAFTING_NATIVE_RECIPE_BINDINGS_V2\n", Encoding.UTF8);
                ProductionRecipeBindingDocument future = Load(path);
                if (future.LoadState != ProductionRecipeBindingLoadState.UnsupportedVersion) return "FAIL production binding future fail closed";
                return "PASS production recipe binding store";
            }
            catch (Exception ex) { return "FAIL production binding store exception " + ex.GetType().Name; }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }
    }
}
