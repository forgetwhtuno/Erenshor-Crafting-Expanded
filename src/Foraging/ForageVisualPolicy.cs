using System;

namespace ErenshorCraftingExpanded
{
    // Pure ranking policy for selecting believable native scene vegetation. Runtime discovery
    // already enforces gameplay-scene ownership and cloneable MeshRenderer/MeshFilter shape.
    // Every specialized family requires explicit name/mesh/hierarchy evidence and never falls back
    // to the generic Wild Herb plant pool.
    public static class ForageVisualPolicy
    {
        public static int ScoreCandidate(string hierarchyPath, string gameObjectName, string meshName, float largestDimension)
        {
            return ScoreCandidate(hierarchyPath, gameObjectName, meshName, largestDimension, ForageResourcePool.OpenHerbs);
        }

        public static int ScoreCandidate(string hierarchyPath, string gameObjectName, string meshName, float largestDimension, ForageResourcePool pool)
        {
            if (float.IsNaN(largestDimension) || float.IsInfinity(largestDimension) || largestDimension < 0.08f || largestDimension > 6f)
                return int.MinValue;

            string text = ((hierarchyPath ?? string.Empty) + " " + (gameObjectName ?? string.Empty) + " " + (meshName ?? string.Empty)).ToLowerInvariant();
            if (ContainsRejectedToken(text, new string[]
            {
                "rock", "rocks", "boulder", "boulders", "ore", "ores", "mineral", "minerals",
                "crystal", "crystals", "cliff", "cliffs", "wall", "walls", "door", "doors", "gate",
                "gates", "bridge", "bridges", "road", "roads", "sign", "barrel", "crate", "chest",
                "pillar", "column", "statue"
            }) || text.IndexOf("oredeposit", StringComparison.Ordinal) >= 0 ||
                 text.IndexOf("mineraldeposit", StringComparison.Ordinal) >= 0)
                return int.MinValue;

            int score = 0;
            if (pool == ForageResourcePool.CoveredFungi)
            {
                if (!AddFungusEvidence(text, ref score)) return int.MinValue;
                if (text.IndexOf("cave", StringComparison.Ordinal) >= 0) score += 10;
                return AddSizeBonus(score, largestDimension);
            }

            if (pool == ForageResourcePool.OpenFlowers)
            {
                bool explicitFlower = false;
                if (text.IndexOf("flower", StringComparison.Ordinal) >= 0) { score += 210; explicitFlower = true; }
                if (text.IndexOf("blossom", StringComparison.Ordinal) >= 0) { score += 200; explicitFlower = true; }
                if (text.IndexOf("bloom", StringComparison.Ordinal) >= 0) { score += 190; explicitFlower = true; }
                if (text.IndexOf("petal", StringComparison.Ordinal) >= 0) { score += 170; explicitFlower = true; }
                if (!explicitFlower) return int.MinValue;
                return AddSizeBonus(score, largestDimension);
            }

            if (pool == ForageResourcePool.CoveredMoss)
            {
                bool explicitMoss = false;
                if (text.IndexOf("moss", StringComparison.Ordinal) >= 0) { score += 210; explicitMoss = true; }
                if (text.IndexOf("lichen", StringComparison.Ordinal) >= 0) { score += 190; explicitMoss = true; }
                if (!explicitMoss) return int.MinValue;
                if (text.IndexOf("cave", StringComparison.Ordinal) >= 0) score += 10;
                return AddSizeBonus(score, largestDimension);
            }

            if (pool == ForageResourcePool.OpenRoots)
            {
                bool explicitRoot = false;
                if (text.IndexOf("root", StringComparison.Ordinal) >= 0) { score += 210; explicitRoot = true; }
                if (text.IndexOf("rhizome", StringComparison.Ordinal) >= 0) { score += 205; explicitRoot = true; }
                if (text.IndexOf("briar", StringComparison.Ordinal) >= 0) { score += 180; explicitRoot = true; }
                if (text.IndexOf("bramble", StringComparison.Ordinal) >= 0) { score += 180; explicitRoot = true; }
                if (text.IndexOf("vine", StringComparison.Ordinal) >= 0) { score += 160; explicitRoot = true; }
                if (text.IndexOf("thorn", StringComparison.Ordinal) >= 0) { score += 150; explicitRoot = true; }
                if (!explicitRoot) return int.MinValue;
                return AddSizeBonus(score, largestDimension);
            }

            // Wild Herb is the only broad plant family. This preserves the live-proven bush
            // fallback and prevents a generic plant from impersonating every later resource.
            if (text.IndexOf("tff_plant", StringComparison.Ordinal) >= 0) score += 150;
            if (text.IndexOf("herb", StringComparison.Ordinal) >= 0) score += 145;
            if (text.IndexOf("plant", StringComparison.Ordinal) >= 0) score += 105;
            if (text.IndexOf("fern", StringComparison.Ordinal) >= 0) score += 90;
            if (text.IndexOf("weed", StringComparison.Ordinal) >= 0) score += 80;
            if (text.IndexOf("grass", StringComparison.Ordinal) >= 0) score += 70;
            if (text.IndexOf("tff_bush", StringComparison.Ordinal) >= 0) score += 60;
            if (text.IndexOf("bush", StringComparison.Ordinal) >= 0) score += 50;
            if (text.IndexOf("shrub", StringComparison.Ordinal) >= 0) score += 45;
            if (text.IndexOf("foliage", StringComparison.Ordinal) >= 0) score += 40;
            if (text.IndexOf("leaf", StringComparison.Ordinal) >= 0) score += 35;
            if (score <= 0) return int.MinValue;
            return AddSizeBonus(score, largestDimension);
        }

        public static string EvidenceDescription(ForageResourcePool pool)
        {
            if (pool == ForageResourcePool.CoveredFungi) return "mushroom/toadstool/fungus/fungi/spore";
            if (pool == ForageResourcePool.OpenFlowers) return "flower/blossom/bloom/petal";
            if (pool == ForageResourcePool.CoveredMoss) return "moss/lichen";
            if (pool == ForageResourcePool.OpenRoots) return "root/rhizome/briar/bramble/vine/thorn";
            return "plant/herb/fern/bush/shrub/grass/foliage/leaf";
        }

        private static bool AddFungusEvidence(string text, ref int score)
        {
            bool explicitFungus = false;
            if (text.IndexOf("mushroom", StringComparison.Ordinal) >= 0) { score += 220; explicitFungus = true; }
            if (text.IndexOf("toadstool", StringComparison.Ordinal) >= 0) { score += 210; explicitFungus = true; }
            if (text.IndexOf("fungus", StringComparison.Ordinal) >= 0) { score += 200; explicitFungus = true; }
            if (text.IndexOf("fungi", StringComparison.Ordinal) >= 0) { score += 200; explicitFungus = true; }
            if (text.IndexOf("spore", StringComparison.Ordinal) >= 0) { score += 160; explicitFungus = true; }
            return explicitFungus;
        }

        private static int AddSizeBonus(int score, float largestDimension)
        {
            if (largestDimension <= 1.5f) score += 20;
            else if (largestDimension <= 3f) score += 10;
            return score;
        }

        private static bool ContainsRejectedToken(string value, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int start = 0;
                while (start < value.Length)
                {
                    int index = value.IndexOf(token, start, StringComparison.Ordinal);
                    if (index < 0) break;
                    int end = index + token.Length;
                    bool leftBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
                    bool rightBoundary = end >= value.Length || !char.IsLetterOrDigit(value[end]);
                    if (leftBoundary && rightBoundary) return true;
                    start = index + 1;
                }
            }
            return false;
        }

        internal static string RunSelfTests()
        {
            int plant = ScoreCandidate("Environment/TFF_Plant_01A", "TFF_Plant_01A", "PlantMesh", 0.8f, ForageResourcePool.OpenHerbs);
            int bush = ScoreCandidate("Environment/TFF_Bush_02A", "TFF_Bush_02A", "BushMesh", 1.6f, ForageResourcePool.OpenHerbs);
            if (plant <= 0 || bush <= 0) return "FAIL verified-style plant/bush names should be accepted for Wild Herb";
            if (plant <= bush) return "FAIL direct plant evidence should outrank bush fallback for Wild Herb";
            if (ScoreCandidate("Environment/Rocks", "Large_Boulder", "Rock_04", 2f, ForageResourcePool.OpenHerbs) != int.MinValue)
                return "FAIL boulder visual should never be selected";
            if (ScoreCandidate("Environment/Forest", "Forest_Plant", "ForestPlantMesh", 0.9f, ForageResourcePool.OpenHerbs) <= 0)
                return "FAIL word fragment 'ore' inside Forest must not reject vegetation";
            if (ScoreCandidate("Environment", "Door", "DoorMesh", 1f, ForageResourcePool.OpenHerbs) != int.MinValue)
                return "FAIL prop visual should never be selected";
            if (ScoreCandidate("Environment", "Fern_A", "FernMesh", 12f, ForageResourcePool.OpenHerbs) != int.MinValue)
                return "FAIL giant vegetation source should be rejected";

            if (ScoreCandidate("Cave/Fungus", "Cave_Mushroom_01", "MushroomMesh", 0.7f, ForageResourcePool.CoveredFungi) <= 0)
                return "FAIL explicit mushroom visual should be accepted";
            if (ScoreCandidate("Cave/Plants", "Fern_A", "FernMesh", 0.7f, ForageResourcePool.CoveredFungi) != int.MinValue)
                return "FAIL fungus must not silently reuse fern";

            if (ScoreCandidate("Fields", "Blue_Flower_A", "FlowerMesh", 0.6f, ForageResourcePool.OpenFlowers) <= 0)
                return "FAIL explicit flower visual should be accepted";
            if (ScoreCandidate("Fields", "Fern_A", "FernMesh", 0.6f, ForageResourcePool.OpenFlowers) != int.MinValue)
                return "FAIL flower family must not reuse generic fern";

            if (ScoreCandidate("Cave", "Cave_Moss_A", "MossPatch", 0.8f, ForageResourcePool.CoveredMoss) <= 0)
                return "FAIL explicit moss visual should be accepted";
            if (ScoreCandidate("Cave", "Mushroom_A", "MushroomMesh", 0.8f, ForageResourcePool.CoveredMoss) != int.MinValue)
                return "FAIL moss family must not reuse fungus";

            if (ScoreCandidate("The Blight", "Twisted_Root_A", "RootMesh", 1.0f, ForageResourcePool.OpenRoots) <= 0)
                return "FAIL explicit root visual should be accepted";
            if (ScoreCandidate("The Blight", "Bush_A", "BushMesh", 1.0f, ForageResourcePool.OpenRoots) != int.MinValue)
                return "FAIL root family must not reuse ordinary bush";

            if (ScoreCandidate("Cave/Rock", "Mushroom_Rock", "RockMesh", 0.7f, ForageResourcePool.CoveredFungi) != int.MinValue)
                return "FAIL geological fungus-looking candidate should remain rejected";
            return "PASS forage visual policy";
        }
    }
}
