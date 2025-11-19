using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize]
namespace n2;

[TestClass]
public class t2
{
	[TestMethod]
	public void m2() { System.Threading.Thread.Sleep(5000); }
}