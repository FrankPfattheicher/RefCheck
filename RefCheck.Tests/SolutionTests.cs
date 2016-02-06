using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RefCheck.Tests
{
    [TestClass]
    public class SolutionTests
    {
        [TestMethod]
        public void ShouldLoadFourProjects()
        {
            var solution = new Solution();
            var loaded = solution.Load(TestSettings.SolutionFileName);
            Assert.IsTrue(loaded);
            Assert.AreEqual(4, solution.Projects.Count);
        }

    }
}
