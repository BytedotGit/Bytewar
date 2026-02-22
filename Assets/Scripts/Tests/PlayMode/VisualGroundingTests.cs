using System.Collections;
using NUnit.Framework;
using ByteWar.Networking;
using UnityEngine;
using UnityEngine.TestTools;

namespace ByteWar.Tests.PlayMode
{
    public class VisualGroundingTests
    {
        [UnityTest]
        public IEnumerator NetworkPlayer_VisualGrounding_CorrectsSmallHover_UsingTerrain()
        {
            // Arrange: flat terrain at y=0.
            var terrainData = new TerrainData
            {
                size = new Vector3(10f, 1f, 10f)
            };
            var terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = "TestTerrain";
            terrainGo.transform.position = Vector3.zero;

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(5f, 0f, 5f);

            // NetworkPlayer requires a handful of components; for this test we only need Awake wiring.
            playerGo.AddComponent<Unity.Netcode.NetworkObject>();
            playerGo.AddComponent<ByteWar.Abilities.AttributeSet>();
            playerGo.AddComponent<ByteWar.Abilities.AbilitySystemComponent>();
            playerGo.AddComponent<ByteWar.Survival.SurvivalStats>();
            playerGo.AddComponent<ByteWar.Survival.InventoryComponent>();
            playerGo.AddComponent<ByteWar.Core.PlayerInputHandler>();
            playerGo.AddComponent<CharacterController>();

            var player = playerGo.AddComponent<NetworkPlayer>();

            // VisualRoot hovering 0.1m above terrain.
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(playerGo.transform);
            visualRoot.transform.localPosition = Vector3.zero;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "HoverCube";
            cube.transform.SetParent(visualRoot.transform);
            cube.transform.localScale = Vector3.one;
            cube.transform.localPosition = new Vector3(0f, 0.6f, 0f); // bounds.min.y = 0.1 when visualRoot.y == 0

            var cubeRenderer = cube.GetComponent<Renderer>();
            Assert.IsNotNull(cubeRenderer);

            yield return null; // allow Awake/initialization

            float beforeMinY = cubeRenderer.bounds.min.y;
            Assert.That(beforeMinY, Is.GreaterThan(0.05f), "Precondition: cube should start hovering above terrain.");

            // Act
            bool applied = player.TryApplyVisualGroundingNow(desiredDelta: -0.035f, maxOffset: 1.0f, out float appliedOffset, out string diag);

            // Assert
            Assert.IsTrue(applied, $"Expected grounding correction to apply. diag={diag}");
            Assert.That(appliedOffset, Is.EqualTo(0.135f).Within(0.04f), $"Expected ~0.135m correction (hover + sink bias). diag={diag}");

            float afterMinY = cubeRenderer.bounds.min.y;
            Assert.That(Mathf.Abs(afterMinY - 0f), Is.LessThan(0.05f), $"Visual bottom should be close to terrain after correction. afterMinY={afterMinY:0.000} diag={diag}");

            // Cleanup
            Object.Destroy(playerGo);
            Object.Destroy(terrainGo);
        }
    }
}
