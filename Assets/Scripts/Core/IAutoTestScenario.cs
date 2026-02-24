using System.Collections;

namespace ByteWar.Core
{
    /// <summary>
    /// Base interface for AutoTester scenarios. Each scenario validates a specific
    /// feature area and emits PASS/FAIL markers to Player.log.
    /// </summary>
    public interface IAutoTestScenario
    {
        /// <summary>Human-readable name used in PASS/FAIL markers (e.g., "Core", "BuildingVisuals").</summary>
        string Name { get; }

        /// <summary>
        /// Runs the scenario checks. Must log "[AutoTester] PASS: {Name}" on success
        /// or "[AutoTester] FAIL: {Name} ..." and call Application.Quit on failure.
        /// Returns true if the scenario passed, false if it already quit.
        /// </summary>
        IEnumerator Run(AutoTesterContext ctx);
    }
}
