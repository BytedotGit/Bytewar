using ByteWar.Networking;
using NUnit.Framework;
using UnityEngine;

namespace ByteWar.Tests.EditMode
{
    public class PlayerMovementFacingTests
    {
        [Test]
        public void ShouldFaceMoveDirectionWhenRmbHeld_WPlusA_ReturnsFalse()
        {
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(-1f, 1f), isMoving: true));
        }

        [Test]
        public void ShouldFaceMoveDirectionWhenRmbHeld_WPlusD_ReturnsFalse()
        {
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(1f, 1f), isMoving: true));
        }

        [Test]
        public void ShouldFaceMoveDirectionWhenRmbHeld_PureStrafe_ReturnsFalse()
        {
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(-1f, 0f), isMoving: true));
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(1f, 0f), isMoving: true));
        }

        [Test]
        public void ShouldFaceMoveDirectionWhenRmbHeld_BackpedalDiagonal_ReturnsFalse()
        {
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(-1f, -1f), isMoving: true));
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(1f, -1f), isMoving: true));
        }

        [Test]
        public void ShouldFaceMoveDirectionWhenRmbHeld_NotMoving_ReturnsFalse()
        {
            Assert.IsFalse(PlayerMovement.ShouldFaceMoveDirectionWhenRmbHeld(new Vector2(-1f, 1f), isMoving: false));
        }
    }
}
