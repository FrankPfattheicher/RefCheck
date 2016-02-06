using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RefCheck.Tests
{
    [TestClass]
    public class CheckingTests
    {
        [TestMethod]
        public void CheckShouldReturnOneWarning()
        {
            var solution = new Solution();
            solution.Load(TestSettings.SolutionFileName);

            var checker = new ReferenceChecker(solution);
            checker.Check();

            Assert.AreEqual(1, solution.Warnings.Count);
        }
    }
}