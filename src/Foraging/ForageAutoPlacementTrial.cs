using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ErenshorCraftingExpanded
{
    // Runtime auto-placement point for the first Wild Herb vertical slice. Wall/rock hits are
    // context anchors only; final placement is resolved onto nearby reachable ground.
    internal sealed class ForageAutoTrialPoint
    {
        internal Vector3 Position;
        internal float RotationY;
        internal bool Covered;
        internal string WallName = string.Empty;
        internal float DistanceFromPlayer;
    }

    internal static class ForageAutoPlacementTrial
    {
        private const int DesiredClusterCount = ForagePlacementPolicy.DesiredClusterCount;
        private const float PlayerMinDistance = 14f;
        private const float WallSearchDistance = 3.5f;
        private const float ClearRadius = 2.25f;
        private const float NearbyActorRadius = 8f;
        private const float CeilingProbeDistance = 10f;

        private static readonly float[] SearchRadii = new float[] { 18f, 26f, 34f, 42f, 50f };

        // Five-clump presentation geometry is centralized in the pure
        // ForagePresentationPolicy so deterministic tests validate the exact runtime layout.

        internal static List<ForageAutoTrialPoint> FindPoints(string scene, Vector3 playerPosition, int generation, out string summary)
        {
            List<ForageAutoTrialPoint> accepted = new List<ForageAutoTrialPoint>();
            summary = string.Empty;

            NavMeshHit playerNav;
            if (!NavMesh.SamplePosition(playerPosition, out playerNav, 5f, NavMesh.AllAreas))
            {
                summary = "player position could not be projected to NavMesh";
                return accepted;
            }

            NPC[] actors;
            try { actors = UnityEngine.Object.FindObjectsOfType<NPC>(); }
            catch { actors = new NPC[0]; }

            int seed = StableHash(scene) ^ (generation * 7919);
            float angleOffset = PositiveMod(seed, 360);
            int candidateCount = 0;
            int wallHits = 0;
            int groundProbes = 0;
            int anchorSurfaceRejects = 0;
            int obstacleGroundRejects = 0;
            int clearCandidates = 0;

            for (int ring = 0; ring < SearchRadii.Length && accepted.Count < DesiredClusterCount; ring++)
            {
                float radius = SearchRadii[ring];
                for (int step = 0; step < 12 && accepted.Count < DesiredClusterCount; step++)
                {
                    candidateCount++;
                    float angle = angleOffset + (step * 30f) + ((ring % 2) * 15f);
                    float radians = angle * Mathf.Deg2Rad;
                    Vector3 raw = playerNav.position + new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);

                    NavMeshHit sampled;
                    if (!NavMesh.SamplePosition(raw, out sampled, 5f, NavMesh.AllAreas)) continue;
                    Vector3 navPoint = sampled.position;
                    if (Vector3.Distance(playerNav.position, navPoint) < PlayerMinDistance) continue;
                    if (!HasCompletePath(playerNav.position, navPoint)) continue;
                    if (TooCloseToActor(navPoint, actors, NearbyActorRadius)) continue;

                    RaycastHit wall;
                    if (!TryFindWall(navPoint, angleOffset + step * 17f, out wall)) continue;
                    wallHits++;

                    Vector3 placement;
                    RaycastHit ground;
                    if (!TryResolveGroundBesideAnchor(
                        wall,
                        out placement,
                        out ground,
                        ref groundProbes,
                        ref anchorSurfaceRejects,
                        ref obstacleGroundRejects)) continue;

                    if (!HasCompletePath(playerNav.position, placement)) continue;
                    if (Vector3.Distance(playerNav.position, placement) < PlayerMinDistance) continue;
                    if (TooCloseToActor(placement, actors, NearbyActorRadius)) continue;
                    if (TooCloseToAccepted(placement, accepted)) continue;
                    if (!HasClearInteractionSpace(placement, wall.collider, ground.collider)) continue;
                    clearCandidates++;

                    Vector3 facing = wall.normal;
                    facing.y = 0f;
                    float yaw = facing.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(facing.normalized, Vector3.up).eulerAngles.y
                        : 0f;

                    accepted.Add(new ForageAutoTrialPoint
                    {
                        Position = placement,
                        RotationY = yaw,
                        Covered = IsCovered(placement),
                        WallName = SafeObjectName(wall.collider),
                        DistanceFromPlayer = Vector3.Distance(playerNav.position, placement)
                    });
                }
            }

            int covered = 0;
            foreach (ForageAutoTrialPoint point in accepted) if (point.Covered) covered++;
            summary = "accepted=" + accepted.Count + "/" + DesiredClusterCount +
                " candidates=" + candidateCount +
                " wallHits=" + wallHits +
                " groundProbes=" + groundProbes +
                " anchorSurfaceRejects=" + anchorSurfaceRejects +
                " obstacleGroundRejects=" + obstacleGroundRejects +
                " clear=" + clearCandidates +
                " open=" + (accepted.Count - covered) +
                " covered=" + covered;

            // One safe cluster is acceptable; the target remains 2-3. Never force an ugly placement just to fill the target.
            if (!ForagePlacementPolicy.IsUsableClusterCount(accepted.Count))
            {
                summary += " outsideUsefulClusterRange=" + ForagePlacementPolicy.MinimumUsefulClusterCount + "-" + ForagePlacementPolicy.DesiredClusterCount;
                accepted.Clear();
            }
            return accepted;
        }

        // A wall/rock raycast is context only. Probe a small deterministic fan outward from that
        // anchor, raycast down to the real surface, then require that surface to remain close to
        // reachable NavMesh. A boulder/prop anchor collider itself (or a direct child/parent
        // collider) is rejected as final ground. Broad Terrain/Ground colliders are the exception:
        // one terrain collider can legitimately provide both a cliff-side anchor and nearby flat
        // ground, so the slope/NavMesh/clearance gates decide that case instead.
        private static bool TryResolveGroundBesideAnchor(
            RaycastHit wall,
            out Vector3 placement,
            out RaycastHit ground,
            ref int groundProbes,
            ref int anchorSurfaceRejects,
            ref int obstacleGroundRejects)
        {
            placement = Vector3.zero;
            ground = new RaycastHit();
            if (wall.collider == null) return false;

            Vector3 outward = wall.normal;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f) return false;
            outward.Normalize();
            Vector3 tangent = new Vector3(-outward.z, 0f, outward.x);

            ForageGroundSample[] samples = ForagePlacementPolicy.GetGroundSamples();
            for (int i = 0; i < samples.Length; i++)
            {
                Vector3 desired = wall.point +
                    outward * samples[i].OutwardDistance +
                    tangent * samples[i].LateralOffset;

                RaycastHit candidateGround;
                groundProbes++;
                if (!TryResolveGround(desired, out candidateGround)) continue;

                bool sameAnchor = IsSameAnchorSurface(wall.collider, candidateGround.collider);
                bool raisedObstacle = IsObviousRaisedObstacleGround(candidateGround.collider);
                bool forbidden = candidateGround.collider == null ||
                    HasForbiddenComponent(candidateGround.collider.gameObject) ||
                    HasForbiddenSurfaceName(candidateGround.collider);
                if (sameAnchor) anchorSurfaceRejects++;
                if (raisedObstacle) obstacleGroundRejects++;

                NavMeshHit nav;
                if (!NavMesh.SamplePosition(candidateGround.point, out nav, 1.10f, NavMesh.AllAreas)) continue;
                Vector3 delta = nav.position - candidateGround.point;
                float horizontalNavOffset = new Vector2(delta.x, delta.z).magnitude;
                float verticalNavOffset = Mathf.Abs(delta.y);
                float slope = Vector3.Angle(candidateGround.normal, Vector3.up);

                if (!ForagePlacementPolicy.AcceptGroundCandidate(
                    slope,
                    horizontalNavOffset,
                    verticalNavOffset,
                    sameAnchor,
                    raisedObstacle,
                    forbidden)) continue;

                placement = new Vector3(nav.position.x, candidateGround.point.y, nav.position.z);
                ground = candidateGround;
                return true;
            }
            return false;
        }

        internal static GameObject BuildTrialClusterVisual(
            string nodeId,
            RendererScanResult source,
            ForageResourceDefinition resource,
            out string summary)
        {
            summary = string.Empty;
            if (source == null || source.SourceObject == null || resource == null)
            {
                summary = "no runtime vegetation source";
                return null;
            }

            float largest = Mathf.Max(source.BoundsSize.x, Mathf.Max(source.BoundsSize.y, source.BoundsSize.z));
            float normalizedScale = ForagePresentationPolicy.CalculateNormalizedClusterScale(largest);
            if (normalizedScale <= 0f)
            {
                summary = "runtime vegetation source has invalid bounds";
                return null;
            }

            GameObject root = new GameObject("ForageCluster_" + nodeId);
            root.layer = source.SourceObject.layer;
            int built = 0;
            int clumpCount = resource.VisualClumpCount;
            if (clumpCount < 2) clumpCount = 2;
            if (clumpCount > ForagePresentationPolicy.PreferredClusterClumpCount)
                clumpCount = ForagePresentationPolicy.PreferredClusterClumpCount;
            float spread = resource.VisualSpreadMultiplier;
            float scaleMultiplier = resource.VisualScaleMultiplier;

            for (int i = 0; i < clumpCount; i++)
            {
                GameObject clone = GameForagingApi.BuildVisualClone(source.SourceObject, nodeId + "_Plant" + i);
                if (clone == null) continue;
                clone.transform.SetParent(root.transform, false);
                clone.transform.localPosition = new Vector3(
                    ForagePresentationPolicy.GetClusterOffsetX(i) * spread,
                    0f,
                    ForagePresentationPolicy.GetClusterOffsetZ(i) * spread);
                clone.transform.localRotation = Quaternion.Euler(0f, ForagePresentationPolicy.GetClusterYaw(i), 0f);
                clone.transform.localScale = Vector3.one *
                    (normalizedScale * scaleMultiplier * ForagePresentationPolicy.GetClusterRelativeScale(i));
                if (!TryGroundVisualClone(clone, root.transform))
                {
                    try { UnityEngine.Object.Destroy(clone); } catch { }
                    continue;
                }
                built++;
            }

            if (built < 2)
            {
                try { UnityEngine.Object.Destroy(root); } catch { }
                summary = "native vegetation source could not produce at least two visual clumps";
                return null;
            }

            summary = "resource=" + resource.DisplayName +
                " source=" + source.GameObjectName +
                " mesh=" + source.MeshName +
                " clumps=" + built +
                " normalizedScale=" + normalizedScale.ToString("F2") +
                " scaleMultiplier=" + scaleMultiplier.ToString("F2") +
                " spreadMultiplier=" + spread.ToString("F2") +
                " targetLargest=" + ForagePresentationPolicy.ClusterTargetLargestDimension.ToString("F2");
            return root;
        }

        private static bool TryGroundVisualClone(GameObject clone, Transform clusterRoot)
        {
            if (clone == null || clusterRoot == null) return false;
            try
            {
                Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
                bool haveBounds = false;
                float minimumY = 0f;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    float value = renderer.bounds.min.y;
                    if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                    if (!haveBounds || value < minimumY) minimumY = value;
                    haveBounds = true;
                }
                if (!haveBounds) return false;

                float correction;
                if (!ForagePresentationPolicy.TryCalculateGroundingOffset(clusterRoot.position.y, minimumY, out correction)) return false;
                Vector3 local = clone.transform.localPosition;
                local.y += correction;
                clone.transform.localPosition = local;
                return true;
            }
            catch { return false; }
        }

        private static bool HasCompletePath(Vector3 from, Vector3 to)
        {
            try
            {
                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)) return false;
                return path.status == NavMeshPathStatus.PathComplete;
            }
            catch { return false; }
        }

        private static bool TryFindWall(Vector3 navPoint, float angleOffset, out RaycastHit best)
        {
            best = new RaycastHit();
            bool found = false;
            float bestDistance = float.MaxValue;
            Vector3 origin = navPoint + Vector3.up * 0.75f;

            for (int i = 0; i < 12; i++)
            {
                float angle = angleOffset + i * 30f;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                RaycastHit hit;
                if (!Physics.Raycast(origin, direction, out hit, WallSearchDistance, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider == null) continue;
                if (Mathf.Abs(hit.normal.y) > 0.40f) continue;
                if (HasForbiddenComponent(hit.collider.gameObject)) continue;
                if (HasForbiddenSurfaceName(hit.collider)) continue;
                if (hit.distance >= bestDistance) continue;
                best = hit;
                bestDistance = hit.distance;
                found = true;
            }
            return found;
        }

        private static bool TryResolveGround(Vector3 point, out RaycastHit ground)
        {
            ground = new RaycastHit();
            Vector3 origin = point + Vector3.up * 3.0f;
            return Physics.Raycast(origin, Vector3.down, out ground, 8f, ~0, QueryTriggerInteraction.Ignore) && ground.collider != null;
        }

        private static bool HasClearInteractionSpace(Vector3 point, Collider wall, Collider ground)
        {
            Collider[] nearby;
            try { nearby = Physics.OverlapSphere(point + Vector3.up * 0.55f, ClearRadius, ~0, QueryTriggerInteraction.Ignore); }
            catch { return false; }

            foreach (Collider other in nearby)
            {
                if (other == null || other == wall || other == ground) continue;
                if (other.attachedRigidbody != null) return false;
                if (HasForbiddenComponent(other.gameObject)) return false;
                if (HasForbiddenSurfaceName(other)) return false;

                try
                {
                    // Use renderer/physics AABB math rather than Collider.ClosestPoint. Several
                    // Erenshor scenery colliders carry negative/non-uniform transforms; Unity can
                    // emit Physics.ClosestPoint warnings for those even though we only need a
                    // conservative clearance rejection. Bounds.ClosestPoint is warning-free and
                    // deliberately errs on the side of rejecting cluttered placement.
                    Vector3 probe = point + Vector3.up * 0.4f;
                    Vector3 closest = other.bounds.ClosestPoint(probe);
                    if ((closest - probe).sqrMagnitude < 0.81f) return false;
                }
                catch { return false; }
            }
            return true;
        }

        private static bool IsSameAnchorSurface(Collider wall, Collider ground)
        {
            if (wall == null || ground == null) return true;
            if (wall == ground || wall.gameObject == ground.gameObject)
                return !IsBroadTerrainSurface(ground);
            Transform wallTransform = wall.transform;
            Transform groundTransform = ground.transform;
            if (wallTransform == null || groundTransform == null) return false;
            try
            {
                if (wallTransform.parent == groundTransform || groundTransform.parent == wallTransform)
                    return !IsBroadTerrainSurface(ground);
            }
            catch { }
            return false;
        }

        private static bool IsBroadTerrainSurface(Collider collider)
        {
            if (collider == null) return false;
            if (collider is TerrainCollider) return true;
            Transform current = collider.transform;
            int depth = 0;
            while (current != null && depth < 3)
            {
                string name = current.name ?? string.Empty;
                if (name.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Landscape", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(name, "Ground", StringComparison.OrdinalIgnoreCase))
                    return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        private static bool HasForbiddenSurfaceName(Collider collider)
        {
            Transform current = collider == null ? null : collider.transform;
            int depth = 0;
            while (current != null && depth < 6)
            {
                if (ForagePlacementPolicy.IsForbiddenSurfaceName(current.name)) return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        private static bool IsObviousRaisedObstacleGround(Collider collider)
        {
            Transform current = collider == null ? null : collider.transform;
            int depth = 0;
            while (current != null && depth < 4)
            {
                if (ForagePlacementPolicy.IsRaisedObstacleSurfaceName(current.name)) return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        private static bool TooCloseToActor(Vector3 point, NPC[] actors, float radius)
        {
            if (actors == null) return false;
            float sqr = radius * radius;
            foreach (NPC actor in actors)
            {
                if (actor == null || actor.transform == null) continue;
                try
                {
                    if ((actor.transform.position - point).sqrMagnitude < sqr) return true;
                }
                catch { }
            }
            return false;
        }

        private static bool TooCloseToAccepted(Vector3 point, List<ForageAutoTrialPoint> accepted)
        {
            foreach (ForageAutoTrialPoint existing in accepted)
            {
                Vector3 delta = existing.Position - point;
                if (!ForagePlacementPolicy.IsClusterSeparated(delta.x, delta.y, delta.z)) return true;
            }
            return false;
        }

        private static bool IsCovered(Vector3 point)
        {
            try
            {
                RaycastHit hit;
                Vector3 origin = point + Vector3.up * 1.2f;
                if (!Physics.Raycast(origin, Vector3.up, out hit, CeilingProbeDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
                string overheadName = BuildShortHierarchyName(hit.collider == null ? null : hit.collider.transform);
                return ForageEnvironmentPolicy.IsCoveredEvidence(true, hit.distance, overheadName);
            }
            catch { return false; }
        }

        private static string BuildShortHierarchyName(Transform transform)
        {
            if (transform == null) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(96);
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < 4)
            {
                if (sb.Length > 0) sb.Append('/');
                sb.Append(current.name);
                current = current.parent;
                depth++;
            }
            return sb.ToString();
        }

        private static bool HasForbiddenComponent(GameObject gameObject)
        {
            Transform current = gameObject == null ? null : gameObject.transform;
            int depth = 0;
            while (current != null && depth < 6)
            {
                Component[] components;
                try { components = current.GetComponents<Component>(); }
                catch { return true; }

                foreach (Component component in components)
                {
                    if (component == null) continue;
                    string name = component.GetType().Name;
                    if (ForagePlacementPolicy.IsForbiddenInteractionComponentName(name)) return true;
                }
                current = current.parent;
                depth++;
            }
            return false;
        }

        private static string SafeObjectName(Collider collider)
        {
            try { return collider == null || collider.gameObject == null ? "(unknown wall)" : collider.gameObject.name; }
            catch { return "(unknown wall)"; }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
                return hash;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
