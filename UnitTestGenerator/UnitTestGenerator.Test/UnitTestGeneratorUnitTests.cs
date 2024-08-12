using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = UnitTestGenerator.Test.CSharpCodeFixVerifier<
    UnitTestGenerator.UnitTestGeneratorAnalyzer,
    UnitTestGenerator.UnitTestGeneratorCodeFixProvider>;

namespace UnitTestGenerator.Test
{
    [TestClass]
    public class UnitTestGeneratorUnitTest
    {
        [TestMethod]
        public async Task TestMethod1()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestMethod2()
        {

        }
    }
}
